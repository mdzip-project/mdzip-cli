#!/usr/bin/env sh
set -eu

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Error: dotnet SDK is required. Install .NET 10 SDK first." >&2
  exit 1
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "Error: curl is required." >&2
  exit 1
fi

if ! command -v tar >/dev/null 2>&1; then
  echo "Error: tar is required." >&2
  exit 1
fi

REPO_OWNER="${REPO_OWNER:-kylemwhite}"
REPO_NAME="${REPO_NAME:-mdz-cli}"
REPO_REF="${REPO_REF:-main}"

INSTALL_ROOT="${INSTALL_ROOT:-$HOME/.local/share/mdz-cli}"
BIN_DIR="${BIN_DIR:-$HOME/.local/bin}"

TMP_DIR="$(mktemp -d)"
ARCHIVE_URL="https://codeload.github.com/$REPO_OWNER/$REPO_NAME/tar.gz/refs/heads/$REPO_REF"

cleanup() {
  rm -rf "$TMP_DIR"
}
trap cleanup EXIT INT TERM

echo "Downloading $REPO_OWNER/$REPO_NAME ($REPO_REF)..."
curl -fsSL "$ARCHIVE_URL" -o "$TMP_DIR/src.tar.gz"

echo "Extracting sources..."
tar -xzf "$TMP_DIR/src.tar.gz" -C "$TMP_DIR"
SRC_DIR="$TMP_DIR/$REPO_NAME-$REPO_REF"

echo "Publishing CLI..."
rm -rf "$INSTALL_ROOT"
mkdir -p "$INSTALL_ROOT"
dotnet publish "$SRC_DIR/src/mdz/mdz.csproj" -c Release -o "$INSTALL_ROOT" >/dev/null

echo "Installing launcher..."
mkdir -p "$BIN_DIR"
cat > "$BIN_DIR/mdz" <<EOF
#!/usr/bin/env sh
exec dotnet "$INSTALL_ROOT/mdz.dll" "\$@"
EOF
chmod +x "$BIN_DIR/mdz"

echo "Installed mdz to: $INSTALL_ROOT"
echo "Launcher: $BIN_DIR/mdz"
echo "If needed, add to PATH: export PATH=\"$BIN_DIR:\$PATH\""
