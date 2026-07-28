#!/usr/bin/env bash
#
# One-time AWS account setup for the aws-poc-microservice-platform CI/CD pipeline.
#
# Creates:
#   1. A GitHub OIDC identity provider (if missing).
#   2. An IAM role that GitHub Actions assumes via OIDC (scoped to this repo).
#   3. A permissions policy on that role.
#   4. The CDK bootstrap stack (CDKToolkit) in the target account/region.
#
# Run this locally with ADMIN (or PowerUser + IAMFullAccess) credentials.
#
# Usage:
#   ./scripts/aws-bootstrap.sh
#
# Environment overrides:
#   AWS_REGION        (default: us-east-1)
#   GITHUB_ORG        (default: pbandi3)
#   GITHUB_REPO       (default: aws-poc-microservice-platform)
#   ROLE_NAME         (default: GitHubActionsDeployRole)
#   PERMISSIONS_MODE  scoped | broad   (default: broad)
#     - broad  : attaches AWS-managed PowerUserAccess + IAMFullAccess (reliable for a POC/demo)
#     - scoped : attaches least-privilege inline policy (assume cdk-* roles + read CFN outputs)
#
set -euo pipefail

AWS_REGION="${AWS_REGION:-us-east-1}"
GITHUB_ORG="${GITHUB_ORG:-pbandi3}"
GITHUB_REPO="${GITHUB_REPO:-aws-poc-microservice-platform}"
# GitHub emits OIDC subjects for this repo using IMMUTABLE numeric ids, e.g.
# repo:pbandi3@309149007/aws-poc-microservice-platform@1314266439:ref:refs/heads/main
# The trust policy must match that form (plain repo:ORG/REPO does NOT match); we also allow
# the plain form as a fallback.
GITHUB_ORG_ID="${GITHUB_ORG_ID:-309149007}"
GITHUB_REPO_ID="${GITHUB_REPO_ID:-1314266439}"
ROLE_NAME="${ROLE_NAME:-GitHubActionsDeployRole}"
PERMISSIONS_MODE="${PERMISSIONS_MODE:-broad}"

OIDC_HOST="token.actions.githubusercontent.com"
OIDC_ARN_SUFFIX="oidc-provider/${OIDC_HOST}"

log() { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
err() { printf '\033[1;31mERROR:\033[0m %s\n' "$*" >&2; }

command -v aws >/dev/null 2>&1 || { err "aws CLI not found"; exit 1; }

ACCOUNT_ID="$(aws sts get-caller-identity --query Account --output text)"
log "Account: ${ACCOUNT_ID} | Region: ${AWS_REGION} | Repo: ${GITHUB_ORG}/${GITHUB_REPO}"

# ---------------------------------------------------------------------------
# 1. GitHub OIDC provider (idempotent)
# ---------------------------------------------------------------------------
OIDC_ARN="arn:aws:iam::${ACCOUNT_ID}:${OIDC_ARN_SUFFIX}"
if aws iam get-open-id-connect-provider --open-id-connect-provider-arn "${OIDC_ARN}" >/dev/null 2>&1; then
  log "OIDC provider already exists: ${OIDC_ARN}"
else
  log "Creating GitHub OIDC provider..."
  # Thumbprint is validated by AWS for this well-known host; value below is the long-standing root CA thumbprint.
  aws iam create-open-id-connect-provider \
    --url "https://${OIDC_HOST}" \
    --client-id-list "sts.amazonaws.com" \
    --thumbprint-list "6938fd4d98bab03faadb97b34396831e3780aea1" >/dev/null
  log "OIDC provider created."
fi

# ---------------------------------------------------------------------------
# 2. IAM role with repo-scoped trust policy (idempotent)
# ---------------------------------------------------------------------------
TRUST_POLICY="$(cat <<JSON
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "Federated": "${OIDC_ARN}" },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": { "${OIDC_HOST}:aud": "sts.amazonaws.com" },
        "StringLike": { "${OIDC_HOST}:sub": [
          "repo:${GITHUB_ORG}@${GITHUB_ORG_ID}/${GITHUB_REPO}@${GITHUB_REPO_ID}:*",
          "repo:${GITHUB_ORG}/${GITHUB_REPO}:*"
        ] }
      }
    }
  ]
}
JSON
)"

if aws iam get-role --role-name "${ROLE_NAME}" >/dev/null 2>&1; then
  log "Role ${ROLE_NAME} exists; updating trust policy..."
  aws iam update-assume-role-policy --role-name "${ROLE_NAME}" \
    --policy-document "${TRUST_POLICY}"
else
  log "Creating role ${ROLE_NAME}..."
  aws iam create-role --role-name "${ROLE_NAME}" \
    --assume-role-policy-document "${TRUST_POLICY}" \
    --description "GitHub Actions OIDC deploy role for ${GITHUB_ORG}/${GITHUB_REPO}" >/dev/null
fi

# ---------------------------------------------------------------------------
# 3. Permissions
# ---------------------------------------------------------------------------
if [ "${PERMISSIONS_MODE}" = "scoped" ]; then
  log "Attaching SCOPED inline permissions policy..."
  SCOPED_POLICY="$(cat <<JSON
{
  "Version": "2012-10-17",
  "Statement": [
    { "Sid": "AssumeCdkBootstrapRoles", "Effect": "Allow", "Action": "sts:AssumeRole", "Resource": "arn:aws:iam::${ACCOUNT_ID}:role/cdk-*" },
    { "Sid": "ReadCloudFormationOutputs", "Effect": "Allow", "Action": ["cloudformation:DescribeStacks","cloudformation:ListStacks","cloudformation:GetTemplate"], "Resource": "*" }
  ]
}
JSON
)"
  aws iam put-role-policy --role-name "${ROLE_NAME}" \
    --policy-name "PocPipelineScoped" \
    --policy-document "${SCOPED_POLICY}"
else
  log "Attaching BROAD managed policies (PowerUserAccess + IAMFullAccess)..."
  aws iam attach-role-policy --role-name "${ROLE_NAME}" \
    --policy-arn "arn:aws:iam::aws:policy/PowerUserAccess"
  aws iam attach-role-policy --role-name "${ROLE_NAME}" \
    --policy-arn "arn:aws:iam::aws:policy/IAMFullAccess"
fi

ROLE_ARN="arn:aws:iam::${ACCOUNT_ID}:role/${ROLE_NAME}"

# ---------------------------------------------------------------------------
# 4. CDK bootstrap (idempotent)
# ---------------------------------------------------------------------------
log "Bootstrapping CDK in aws://${ACCOUNT_ID}/${AWS_REGION}..."
npx --yes aws-cdk@2 bootstrap "aws://${ACCOUNT_ID}/${AWS_REGION}"

# ---------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------
cat <<SUMMARY

============================================================
 Setup complete.

 Add these GitHub repository secrets
 (Settings -> Secrets and variables -> Actions):

   AWS_ROLE_ARN = ${ROLE_ARN}

 Region is pinned to ${AWS_REGION} in the workflows (env.AWS_REGION).
 Permissions mode used: ${PERMISSIONS_MODE}
============================================================
SUMMARY
