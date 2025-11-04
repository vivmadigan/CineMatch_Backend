# ?? Code Coverage Guide

This project uses **Coverlet** for code coverage analysis and **ReportGenerator** for HTML reports.

---

## ?? **Coverage Goals**

- **Target:** ? 80% line coverage
- **Strategy:** Gradual increase from baseline
- **Enforcement:** CI/CD pipeline blocks builds below threshold

---

## ?? **Quick Start**

### **Windows (PowerShell)**
```powershell
./coverage.ps1
```

### **Linux/Mac (Bash)**
```bash
chmod +x coverage.sh
./coverage.sh
```

### **With Threshold Enforcement**
```powershell
# Fail build if coverage < 80%
./coverage.ps1 80
```

---

## ?? **What Gets Measured**

### **? Included:**
- `Infrastructure` project (services, business logic)
- `Presentation` project (controllers, hubs)

### **? Excluded:**
- **EF Migrations** - Generated code
- **DTOs/Models** - No business logic
- **Entities** - POCOs (Plain Old CLR Objects)
- **DbContext** - EF boilerplate
- **Program.cs/Startup.cs** - Composition root
- **Test projects** - xUnit infrastructure

---

## ??? **Manual Commands**

### **Run Tests with Coverage (No Report)**
```bash
dotnet test /p:CollectCoverage=true
```

### **Run Tests with Threshold Enforcement**
```bash
dotnet test /p:CollectCoverage=true /p:Threshold=80 /p:ThresholdType=line
```

### **Generate HTML Report (After Running Tests)**
```bash
# Install ReportGenerator (one-time)
dotnet tool install --global dotnet-reportgenerator-globaltool

# Generate report
dotnet reportgenerator \
  -reports:**/coverage.cobertura.xml \
  -targetdir:coverage-report \
  -reporttypes:Html
```

---

## ?? **Output Locations**

| File/Folder | Description |
|-------------|-------------|
| `TestResults/coverage/` | Raw coverage data (per test project) |
| `coverage-report/` | HTML report (open `index.html` in browser) |
| `*.cobertura.xml` | Coverage data in Cobertura format |

**Note:** All coverage outputs are `.gitignore`'d

---

## ?? **How Coverage Works**

### **1. Instrumentation**
Coverlet instruments your code during test execution to track which lines are hit.

### **2. Execution**
Tests run normally, Coverlet records which lines executed.

### **3. Analysis**
Coverlet calculates:
- **Line Coverage:** % of executable lines hit
- **Branch Coverage:** % of decision points (if/else) hit
- **Method Coverage:** % of methods called

### **4. Reporting**
ReportGenerator converts raw data into visual HTML reports.

---

## ?? **Reading the HTML Report**

### **Summary Page**
- **Green:** Good coverage (>80%)
- **Yellow:** Moderate coverage (60-80%)
- **Red:** Low coverage (<60%)

### **Drill-Down**
- Click project ? namespace ? class ? method
- **Green lines:** Covered by tests
- **Red lines:** Never executed
- **Orange lines:** Partially covered (branches)

---

## ?? **Configuration**

Coverage settings are centralized in `Directory.Build.props`:

```xml
<PropertyGroup Condition="'$(IsTestProject)' == 'true'">
  <!-- Enable coverage -->
  <CollectCoverage>true</CollectCoverage>
  
  <!-- Output format -->
  <CoverletOutputFormat>cobertura,json</CoverletOutputFormat>
  
  <!-- Threshold (0 = no enforcement locally) -->
  <Threshold>0</Threshold>
  
  <!-- Exclusions -->
  <Exclude>
    [*]*.Migrations.*,
    [*]*.Models.*,
    ...
  </Exclude>
</PropertyGroup>
```

**To change threshold globally:** Edit `<Threshold>80</Threshold>` in `Directory.Build.props`

---

## ?? **CI/CD Integration**

### **Current Status:**
? **Local Coverage:** Working (run scripts)  
? **CI Enforcement:** Coming soon (GitHub Actions)

### **Planned GitHub Actions Workflow:**

```yaml
name: Test & Coverage

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
  with:
          dotnet-version: '9.0.x'
      
      - name: Restore
        run: dotnet restore
      
      - name: Run Tests with Coverage (80% threshold)
        run: |
          dotnet test \
        /p:CollectCoverage=true \
  /p:Threshold=80 \
         /p:ThresholdType=line \
   --no-restore
    
      - name: Generate Coverage Report
   if: always()
  run: |
    dotnet tool install --global dotnet-reportgenerator-globaltool
          dotnet reportgenerator \
            -reports:**/coverage.cobertura.xml \
            -targetdir:coverage-report \
          -reporttypes:Html
      
      - name: Upload Coverage Report
        if: always()
        uses: actions/upload-artifact@v4
        with:
       name: coverage-report
          path: coverage-report/
```

---

## ?? **Coverage Roadmap**

### **Phase 1: Baseline (Week 1)** ?
- [x] Add Coverlet to test projects
- [x] Create coverage scripts
- [x] Generate first report
- [ ] **Action:** Run `./coverage.ps1` and note baseline%

### **Phase 2: Improve Coverage (Week 2)**
- [ ] Set threshold to baseline + 10%
- [ ] Add tests for uncovered critical paths
- [ ] Focus on services and business logic

### **Phase 3: Enforce 80% (Week 3)**
- [ ] Update threshold to 80%
- [ ] Add CI enforcement
- [ ] Block PRs with insufficient coverage

---

## ?? **Troubleshooting**

### **"No coverage files found"**
**Cause:** Tests didn't run or coverage not collected

**Fix:**
```bash
# Ensure coverlet.msbuild is installed
dotnet restore

# Run tests with explicit coverage flag
dotnet test /p:CollectCoverage=true
```

---

### **"Threshold not met" (build fails)**
**Cause:** Coverage below configured threshold

**Fix Options:**
1. **Add more tests** (preferred)
2. **Lower threshold temporarily** (edit `Directory.Build.props`)
3. **Check which code is uncovered** (open HTML report)

---

### **Scripts don't run (permission denied)**
**Linux/Mac:**
```bash
chmod +x coverage.sh
./coverage.sh
```

**Windows:** PowerShell execution policy
```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
./coverage.ps1
```

---

### **ReportGenerator not found**
**Fix:**
```bash
dotnet tool install --global dotnet-reportgenerator-globaltool
```

---

## ?? **Best Practices**

### **? DO:**
- **Test business logic** (services, domain models)
- **Test critical paths** (authentication, payment, data integrity)
- **Test edge cases** (null inputs, boundary conditions)
- **Run coverage locally** before pushing

### **? DON'T:**
- **Test DTOs/POCOs** (no logic to test)
- **Test generated code** (migrations, scaffolding)
- **Chase 100% coverage** (diminishing returns)
- **Test implementation details** (private methods)

---

## ?? **Resources**

- **Coverlet Docs:** https://github.com/coverlet-coverage/coverlet
- **ReportGenerator Docs:** https://github.com/danielpalme/ReportGenerator
- **xUnit Docs:** https://xunit.net/
- **FluentAssertions:** https://fluentassertions.com/

---

## ?? **Getting Help**

1. **Check HTML report** ? See what's uncovered
2. **Check `Directory.Build.props`** ? Verify exclusions
3. **Run with verbose logging:**
   ```bash
   dotnet test --verbosity detailed /p:CollectCoverage=true
   ```

---

## ?? **Summary**

| Command | Purpose |
|---------|---------|
| `./coverage.ps1` | Run tests + generate HTML report (Windows) |
| `./coverage.sh` | Run tests + generate HTML report (Linux/Mac) |
| `./coverage.ps1 80` | Enforce 80% threshold |
| `dotnet test /p:CollectCoverage=true` | Collect coverage (no report) |

**Next Steps:**
1. ? Run `./coverage.ps1` to get baseline
2. ? Open `coverage-report/index.html` in browser
3. ? Note current coverage %
4. ? Update threshold in `Directory.Build.props` to current% + 10%
5. ? Add CI enforcement (GitHub Actions)

---

**Happy Testing! ??**
