#!/usr/bin/env sh
set -eu

if ! command -v curl >/dev/null 2>&1; then
  echo "Error: curl is required." >&2
  exit 1
fi

if ! command -v tar >/dev/null 2>&1; then
  echo "Error: tar is required." >&2
  exit 1
fi

if ! command -v uname >/dev/null 2>&1; then
  echo "Error: uname is required." >&2
  exit 1
fi

REPO_OWNER="${REPO_OWNER:-mdzip-project}"
REPO_NAME="${REPO_NAME:-mdzip-cli}"
MDZ_VERSION="${MDZ_VERSION:-}"
GITHUB_TOKEN="${GITHUB_TOKEN:-}"

if [ "$(id -u)" -eq 0 ]; then
  INSTALL_ROOT="${INSTALL_ROOT:-/usr/local/lib/mdz-cli}"
  BIN_DIR="${BIN_DIR:-/usr/local/bin}"
else
  INSTALL_ROOT="${INSTALL_ROOT:-$HOME/.local/share/mdz-cli}"
  BIN_DIR="${BIN_DIR:-$HOME/.local/bin}"
fi

OS="$(uname -s)"
ARCH="$(uname -m)"

case "$OS" in
  Linux) OS_PART="linux" ;;
  Darwin) OS_PART="osx" ;;
  *)
    echo "Error: unsupported OS '$OS'." >&2
    exit 1
    ;;
esac

case "$ARCH" in
  x86_64|amd64) ARCH_PART="x64" ;;
  arm64|aarch64) ARCH_PART="arm64" ;;
  *)
    echo "Error: unsupported architecture '$ARCH'." >&2
    exit 1
    ;;
esac

RID="$OS_PART-$ARCH_PART"

auth_header() {
  if [ -n "$GITHUB_TOKEN" ]; then
    printf '%s' "Authorization: Bearer $GITHUB_TOKEN"
  fi
}

# Fetch a GitHub URL, falling back to an unauthenticated request if an ambient
# GITHUB_TOKEN is present but rejected (expired/invalid). mdzip-cli is a public
# repo, so a token is never required to install.
# Usage: gh_get URL [OUTFILE]   (no OUTFILE => body written to stdout)
gh_get() {
  _url="$1"
  _out="${2:-}"
  if [ -n "$_out" ]; then
    if [ -n "$GITHUB_TOKEN" ]; then
      if curl -fL -H "$(auth_header)" "$_url" -o "$_out"; then return 0; fi
      echo "Warning: GitHub request failed with GITHUB_TOKEN; retrying without authentication (token may be expired/invalid)." >&2
    fi
    curl -fL "$_url" -o "$_out"
  else
    if [ -n "$GITHUB_TOKEN" ]; then
      if _body="$(curl -fsSL -H "$(auth_header)" "$_url")"; then printf '%s' "$_body"; return 0; fi
      echo "Warning: GitHub request failed with GITHUB_TOKEN; retrying without authentication (token may be expired/invalid)." >&2
    fi
    curl -fsSL "$_url"
  fi
}

TMP_DIR="$(mktemp -d)"
cleanup() {
  rm -rf "$TMP_DIR"
}
trap cleanup EXIT INT TERM

if [ -z "$MDZ_VERSION" ]; then
  API_URL="https://api.github.com/repos/$REPO_OWNER/$REPO_NAME/releases/latest"
  MDZ_VERSION="$(gh_get "$API_URL" | sed -n 's/.*"tag_name":[[:space:]]*"\([^"]*\)".*/\1/p' | head -n 1)"
fi

if [ -z "$MDZ_VERSION" ]; then
  echo "Error: could not determine release version. Set MDZ_VERSION (for example: v1.0.0)." >&2
  exit 1
fi

ASSET_NAME="mdz-$MDZ_VERSION-$RID.tar.gz"
DOWNLOAD_URL="https://github.com/$REPO_OWNER/$REPO_NAME/releases/download/$MDZ_VERSION/$ASSET_NAME"

echo "Installing $REPO_OWNER/$REPO_NAME $MDZ_VERSION ($RID)..."
gh_get "$DOWNLOAD_URL" "$TMP_DIR/mdz.tar.gz"

rm -rf "$INSTALL_ROOT"
mkdir -p "$INSTALL_ROOT"
tar -xzf "$TMP_DIR/mdz.tar.gz" -C "$INSTALL_ROOT"
chmod +x "$INSTALL_ROOT/mdz"

mkdir -p "$BIN_DIR"
cat > "$BIN_DIR/mdz" <<EOF
#!/usr/bin/env sh
exec "$INSTALL_ROOT/mdz" "\$@"
EOF
chmod +x "$BIN_DIR/mdz"

echo "Installed files: $INSTALL_ROOT"
echo "Launcher: $BIN_DIR/mdz"
if command -v mdz >/dev/null 2>&1; then
  echo "mdz is available on PATH."
else
  echo "If needed, add to PATH: export PATH=\"$BIN_DIR:\$PATH\""
fi
