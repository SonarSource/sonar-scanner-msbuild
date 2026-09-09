#!/usr/bin/env bash
# Probe the Artifactory Edge node until a Vault-issued token is federated
# (HTTP 200). 401/connection-failure/429/5xx retries for up to ~2 minutes.
set -euo pipefail

: "${ARTIFACTORY_ACCESS_TOKEN:?ARTIFACTORY_ACCESS_TOKEN must be set before waiting for token federation}"

ARTIFACTORY_URL="${ARTIFACTORY_URL:-https://repox-internal.dev.sonar.build/artifactory}"

# DEV distributions live in sonarsource-builds; LTA/LATEST_RELEASE in
# sonarsource-releases. Federation can return 200 on one virtual repo while
# the other still 401s, so probe both unless the caller overrides PROBE_PATH.
if [[ -n "${PROBE_PATH:-}" ]]; then
  PROBE_PATHS=("${PROBE_PATH}")
else
  PROBE_PATHS=(
    "api/search/versions?g=com.sonarsource.sonarqube&a=sonarqube-developer&remote=1&repos=sonarsource-releases&v=*"
    "api/search/versions?g=com.sonarsource.sonarqube&a=sonarqube-developer&remote=1&repos=sonarsource-builds&v=*"
  )
fi

is_retryable() {
  local status="$1"
  [[ "${status}" == "401" || "${status}" == "000" || "${status}" == "429" || "${status}" =~ ^5[0-9][0-9]$ ]]
}

for attempt in {1..12}; do
  all_ok=true
  last_status=""
  for path in "${PROBE_PATHS[@]}"; do
    status=$(curl --silent --output /dev/null --write-out "%{http_code}" \
      --connect-timeout 5 --max-time 20 \
      --header "Authorization: Bearer ${ARTIFACTORY_ACCESS_TOKEN}" \
      "${ARTIFACTORY_URL}/${path}" || true)
    last_status="${status}"
    if [[ "${status}" == "200" ]]; then
      continue
    fi
    all_ok=false
    if ! is_retryable "${status}"; then
      echo "Unexpected response from Edge: HTTP ${status}"
      exit 1
    fi
  done
  if [[ "${all_ok}" == "true" ]]; then
    echo "Artifactory token accepted by Edge"
    exit 0
  fi
  echo "Waiting for Artifactory token federation (attempt ${attempt}/12, HTTP ${last_status})"
  if [[ "${attempt}" -lt 12 ]]; then
    sleep 10
  fi
done

echo "Artifactory token was not accepted by Edge within 2 minutes"
exit 1
