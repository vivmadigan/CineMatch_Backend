#!/usr/bin/env bash
# ========================================
# Code Coverage Report Generator (Bash)
# ========================================
# Runs tests with coverage, generates HTML report
# Usage: ./coverage.sh [threshold]
#        ./coverage.sh 80  # Enforce 80% coverage
# ========================================

set -e  # Exit on error

THRESHOLD=${1:-0}  # Default: no threshold enforcement

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
GRAY='\033[0;90m'
NC='\033[0m' # No Color

echo ""
echo -e "${CYAN}========================================"
echo -e " CineMatch Code Coverage"
echo -e "========================================${NC}"
echo ""

# 1. Clean previous coverage results
echo -e "${YELLOW}?? Cleaning previous coverage results...${NC}"
rm -rf ./TestResults
rm -rf ./coverage-report

# 2. Restore packages
echo -e "${YELLOW}?? Restoring NuGet packages...${NC}"
dotnet restore

# 3. Run tests with coverage
echo ""
echo -e "${YELLOW}?? Running tests with coverage collection...${NC}"
echo -e "${GRAY}   Excluding: Migrations, DTOs, Entities, Models${NC}"

# Define exclusions (same as Directory.Build.props)
EXCLUDE_FILTERS="[*]*.Migrations.*,[*]*.Program,[*]*.Startup,[*]*.Data.Context.*,[*]*.Data.Entities.*,[*]*.Models.*,[*]*.Options.*,[*]*Dto,[*]*Request,[*]*Response"
EXCLUDE_BY_FILE="**/Migrations/**,**/obj/**,**/bin/**,**/*Designer.cs,**/*.g.cs,**/*.g.i.cs"

if [ "$THRESHOLD" -gt 0 ]; then
 echo -e "${MAGENTA}   Threshold: ${THRESHOLD}% (build will fail if below)${NC}"
    dotnet test \
        /p:CollectCoverage=true \
      /p:CoverletOutputFormat=cobertura \
     /p:Exclude="$EXCLUDE_FILTERS" \
        /p:ExcludeByFile="$EXCLUDE_BY_FILE" \
     /p:Threshold=$THRESHOLD \
        /p:ThresholdType=line \
        /p:ThresholdStat=total \
     --no-restore \
        --verbosity minimal
else
    echo -e "${GREEN}   Threshold: None (report only)${NC}"
    dotnet test \
        /p:CollectCoverage=true \
      /p:CoverletOutputFormat=cobertura \
  /p:Exclude="$EXCLUDE_FILTERS" \
        /p:ExcludeByFile="$EXCLUDE_BY_FILE" \
        --no-restore \
  --verbosity minimal
fi

# 4. Find coverage files
echo ""
echo -e "${YELLOW}?? Locating coverage files...${NC}"
COVERAGE_FILES=$(find . -name "coverage.cobertura.xml" -type f)

if [ -z "$COVERAGE_FILES" ]; then
    echo -e "${RED}? No coverage files found!${NC}"
    echo -e "${YELLOW}   Expected: **/TestResults/coverage/coverage.cobertura.xml${NC}"
    exit 1
fi

FILE_COUNT=$(echo "$COVERAGE_FILES" | wc -l)
echo -e "${GREEN}   Found $FILE_COUNT coverage file(s)${NC}"
echo "$COVERAGE_FILES" | while read -r file; do
    echo -e "${GRAY}   - $file${NC}"
done

# 5. Install ReportGenerator (if not already installed)
echo ""
echo -e "${YELLOW}?? Ensuring ReportGenerator is installed...${NC}"

if ! dotnet tool list --global | grep -q "dotnet-reportgenerator-globaltool"; then
    echo -e "${YELLOW}   Installing ReportGenerator...${NC}"
    dotnet tool install --global dotnet-reportgenerator-globaltool
else
    echo -e "${GREEN}   ReportGenerator is already installed${NC}"
fi

# 6. Generate HTML report
echo ""
echo -e "${YELLOW}?? Generating HTML coverage report...${NC}"

# Convert file list to semicolon-separated string
REPORTS_ARG=$(echo "$COVERAGE_FILES" | tr '\n' ';' | sed 's/;$//')

dotnet reportgenerator \
    "-reports:$REPORTS_ARG" \
    "-targetdir:./coverage-report" \
    "-reporttypes:Html;TextSummary" \
    "-title:CineMatch Code Coverage"

# 7. Display summary
echo ""
echo -e "${GREEN}========================================"
echo -e " ? Coverage Report Generated!"
echo -e "========================================${NC}"
echo ""
echo -e "${CYAN}?? HTML Report: ./coverage-report/index.html${NC}"
echo -e "${CYAN}?? Coverage Files: $FILE_COUNT file(s) in ./TestResults/${NC}"
echo ""
echo -e "${YELLOW}?? To view the report, open:${NC}"
echo -e "${CYAN}   file://$(pwd)/coverage-report/index.html${NC}"
echo ""

# 8. Try to open in browser (Linux/Mac)
if command -v xdg-open &> /dev/null; then
    echo -e "${YELLOW}?? Opening report in browser...${NC}"
    xdg-open ./coverage-report/index.html
elif command -v open &> /dev/null; then
    echo -e "${YELLOW}?? Opening report in browser...${NC}"
    open ./coverage-report/index.html
else
echo -e "${YELLOW}?? Manually open the file above in your browser${NC}"
fi

echo ""
echo -e "${GREEN}? Done!${NC}"
echo ""
