#!/usr/bin/env bash
# Probe the internal Artifactory Edge node until a Vault-issued token is
# federated (HTTP 200). 401/connection-failure retries for up to ~2 minutes.
set -euo pipefail

: "${ARTIFACTORY_ACCESS_TOKEN:?ARTIFACTORY_ACCESS_TOKEN must be set before waiting for token federation}"

ARTIFACTORY_URL="${ARTIFACTORY_URL:-https://repox-internal.dev.sonar.build/artifactory}"
PROBE_PATH="${PROBE_PATH:-api/search/versions?g=com.sonarsource.sonarqube&a=sonarqube-developer&remote=1&repos=sonarsource-releases&v=*}"

for attempt in {1..12}; do
  status=$(curl --silent --output /dev/null --write-out "%{http_code}" \
    --header "Authorization: Bearer ${ARTIFACTORY_ACCESS_TOKEN}" \
    "${ARTIFACTORY_URL}/${PROBE_PATH}" || true)
  if [[ "${status}" == "200" ]]; then
    echo "Artifactory token accepted by Edge"
    exit 0
  fi
  if [[ "${status}" != "401" && "${status}" != "000" ]]; then
    echo "Unexpected response from Edge: HTTP ${status}"
    exit 1
  fi
  echo "Waiting for Artifactory token federation (attempt ${attempt}/12, HTTP ${status})"
  sleep 10
done

echo "Artifactory token was not accepted by Edge within 2 minutes"
exit 1
