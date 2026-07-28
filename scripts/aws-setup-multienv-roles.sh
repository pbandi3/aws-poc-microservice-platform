#!/usr/bin/env bash
#
# Provisions TWO GitHub OIDC deploy roles to simulate multi-environment segregation of
# duties inside a single AWS account:
#
#   1. Non-prod role  (dev, ephemeral PR envs, cleanup)  - assumable from any ref/environment.
#   2. Prod role      (production releases)              - assumable ONLY from the protected
#                                                          'prod' GitHub Environment.
#
# This demonstrates the real-world pattern where non-prod and prod use different credentials
# (typically different accounts). Here we use two roles + GitHub Environment scoping so the
# same account can safely stand in for both, and prod creds are never exposed to PR builds.
#
# Run locally with ADMIN (or PowerUser + IAMFullAccess) credentials. Idempotent.
#
# Usage:
#   ./scripts/aws-setup-multienv-roles.sh
#
# Environment overrides:
#   AWS_REGION        (default: us-east-1)
#   GITHUB_ORG        (default: pbandi3)
#   GITHUB_REPO       (default: aws-poc-microservice-platform)
#   NONPROD_ROLE_NAME (default: GitHubActionsDeployRole)        # reuses your existing role
#   PROD_ROLE_NAME    (default: GitHubActionsProdDeployRole)
#   PROD_ENVIRONMENT  (default: prod)                            # GitHub Environment name
#
set -euo pipefail

AWS_REGION="${AWS_REGION:-us-east-1}"
GITHUB_ORG="${GITHUB_ORG:-pbandi3}"
GITHUB_REPO="${GITHUB_REPO:-aws-poc-microservice-platform}"
# GitHub issues OIDC tokens for THIS repo with IMMUTABLE numeric ids in the subject claim,
# e.g. repo:pbandi3@309149007/aws-poc-microservice-platform@1314266439:environment:dev.
# The trust policies must match that exact form (the plain repo:ORG/REPO:... form does NOT
# match). We also allow the plain form as a fallback in case GitHub emits it.
GITHUB_ORG_ID="${GITHUB_ORG_ID:-309149007}"
GITHUB_REPO_ID="${GITHUB_REPO_ID:-1314266439}"
NONPROD_ROLE_NAME="${NONPROD_ROLE_NAME:-GitHubActionsDeployRole}"
PROD_ROLE_NAME="${PROD_ROLE_NAME:-GitHubActionsProdDeployRole}"
PROD_ENVIRONMENT="${PROD_ENVIRONMENT:-prod}"

OIDC_HOST="token.actions.githubusercontent.com"

# Subject prefixes: immutable (authoritative) + plain (fallback).
SUBJECT_IMMUTABLE="repo:${GITHUB_ORG}@${GITHUB_ORG_ID}/${GITHUB_REPO}@${GITHUB_REPO_ID}"
SUBJECT_PLAIN="repo:${GITHUB_ORG}/${GITHUB_REPO}"

log() { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
err() { printf '\033[1;31mERROR:\033[0m %s\n' "$*" >&2; }

command -v aws >/dev/null 2>&1 || { err "aws CLI not found"; exit 1; }

ACCOUNT_ID="$(aws sts get-caller-identity --query Account --output text)"
OIDC_ARN="arn:aws:iam::${ACCOUNT_ID}:oidc-provider/${OIDC_HOST}"
log "Account: ${ACCOUNT_ID} | Repo: ${GITHUB_ORG}/${GITHUB_REPO}"

if ! aws iam get-open-id-connect-provider --open-id-connect-provider-arn "${OIDC_ARN}" >/dev/null 2>&1; then
  err "OIDC provider ${OIDC_ARN} not found. Run scripts/aws-bootstrap.sh first."
  exit 1
fi

# ---------------------------------------------------------------------------
# Helper: create/update a role with a trust policy + broad demo permissions.
# ---------------------------------------------------------------------------
ensure_role() {
  local role_name="$1" trust_policy="$2"
  if aws iam get-role --role-name "${role_name}" >/dev/null 2>&1; then
    log "Role ${role_name} exists; updating trust policy..."
    aws iam update-assume-role-policy --role-name "${role_name}" --policy-document "${trust_policy}"
  else
    log "Creating role ${role_name}..."
    aws iam create-role --role-name "${role_name}" \
      --assume-role-policy-document "${trust_policy}" \
      --description "GitHub Actions OIDC deploy role for ${GITHUB_ORG}/${GITHUB_REPO}" >/dev/null
  fi
  # Broad permissions keep the POC reliable; tighten for real workloads.
  aws iam attach-role-policy --role-name "${role_name}" \
    --policy-arn "arn:aws:iam::aws:policy/PowerUserAccess" || true
  aws iam attach-role-policy --role-name "${role_name}" \
    --policy-arn "arn:aws:iam::aws:policy/IAMFullAccess" || true
}

# ---------------------------------------------------------------------------
# 1. Non-prod role: any ref/environment in this repo.
# ---------------------------------------------------------------------------
NONPROD_TRUST="$(cat <<JSON
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Principal": { "Federated": "${OIDC_ARN}" },
    "Action": "sts:AssumeRoleWithWebIdentity",
    "Condition": {
      "StringEquals": { "${OIDC_HOST}:aud": "sts.amazonaws.com" },
      "StringLike": { "${OIDC_HOST}:sub": ["${SUBJECT_IMMUTABLE}:*", "${SUBJECT_PLAIN}:*"] }
    }
  }]
}
JSON
)"
ensure_role "${NONPROD_ROLE_NAME}" "${NONPROD_TRUST}"

# ---------------------------------------------------------------------------
# 2. Prod role: ONLY from the protected 'prod' GitHub Environment.
# ---------------------------------------------------------------------------
PROD_TRUST="$(cat <<JSON
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Principal": { "Federated": "${OIDC_ARN}" },
    "Action": "sts:AssumeRoleWithWebIdentity",
    "Condition": {
      "StringEquals": {
        "${OIDC_HOST}:aud": "sts.amazonaws.com",
        "${OIDC_HOST}:sub": [
          "${SUBJECT_IMMUTABLE}:environment:${PROD_ENVIRONMENT}",
          "${SUBJECT_PLAIN}:environment:${PROD_ENVIRONMENT}"
        ]
      }
    }
  }]
}
JSON
)"
ensure_role "${PROD_ROLE_NAME}" "${PROD_TRUST}"

NONPROD_ARN="arn:aws:iam::${ACCOUNT_ID}:role/${NONPROD_ROLE_NAME}"
PROD_ARN="arn:aws:iam::${ACCOUNT_ID}:role/${PROD_ROLE_NAME}"

cat <<SUMMARY

============================================================
 Two-role setup complete.

 NON-PROD role: ${NONPROD_ARN}
 PROD role:     ${PROD_ARN}

 Configure GitHub secrets so each environment uses the right role
 (Settings -> Environments, and Settings -> Secrets and variables -> Actions):

   Repository secret (used by pr-cleanup, which has no environment):
     AWS_ROLE_ARN = ${NONPROD_ARN}

   Environment 'dev'       -> secret AWS_ROLE_ARN = ${NONPROD_ARN}
   Environment 'ephemeral' -> secret AWS_ROLE_ARN = ${NONPROD_ARN}
   Environment 'prod'      -> secret AWS_ROLE_ARN = ${PROD_ARN}
                              (add a Required reviewer to 'prod' to gate releases)

 The prod role's trust policy rejects any OIDC token whose subject is not
 'repo:${GITHUB_ORG}/${GITHUB_REPO}:environment:${PROD_ENVIRONMENT}', so PR/feature
 builds physically cannot assume production credentials.
============================================================
SUMMARY
