# ? Coverlet Implementation Checklist

Use this checklist to verify and activate your code coverage setup.

---

## ?? **Phase 1: Verification** (5 minutes)

### **? Step 1: Verify Files Exist**
- [ ] `Directory.Build.props` exists in solution root
- [ ] `coverage.ps1` exists (Windows script)
- [ ] `coverage.sh` exists (Linux/Mac script)
- [ ] `COVERAGE.md` exists (comprehensive guide)
- [ ] `COVERAGE_QUICKSTART.md` exists (quick start)
- [ ] `.github/workflows/test-coverage.yml` exists

### **? Step 2: Verify Packages**
Open test project files and verify:

**Infrastructure.Tests/Infrastructure.Tests.csproj:**
- [ ] Contains `<PackageReference Include="coverlet.msbuild" Version="6.0.2" />`

**Presentation.Tests/Presentation.Tests.csproj:**
- [ ] Contains `<PackageReference Include="coverlet.msbuild" Version="6.0.2" />`

### **? Step 3: Restore Packages**
```bash
dotnet restore
```
- [ ] Restore completes successfully
- [ ] No errors about missing packages

---

## ?? **Phase 2: Baseline Check** (10 minutes)

### **? Step 4: Run Coverage Script**

**Windows (PowerShell):**
```powershell
./coverage.ps1
```

**Linux/Mac (Bash):**
```bash
chmod +x coverage.sh
./coverage.sh
```

**Expected Behavior:**
- [ ] Script runs without errors
- [ ] Tests execute successfully
- [ ] HTML report generates
- [ ] Browser opens automatically with report
- [ ] `coverage-report/index.html` exists

### **? Step 5: Record Baseline**

Open `coverage-report/index.html` and note:

- **Line Coverage:** ____%
- **Branch Coverage:** ____%
- **Method Coverage:** ____%

**Example:** Line Coverage: 67.3%

---

## ?? **Phase 3: Configure Threshold** (2 minutes)

### **? Step 6: Set Initial Threshold**

1. **Open:** `Directory.Build.props`
2. **Find:** `<Threshold>0</Threshold>`
3. **Update to:** Baseline + 10%
   - Example: If baseline = 67%, set to 75%
   ```xml
   <Threshold>75</Threshold>
   ```
4. **Save file**

### **? Step 7: Test Threshold**

```powershell
# Should PASS if coverage ? threshold
./coverage.ps1 75
```

**Expected:**
- [ ] Build succeeds (if coverage ? 75%)
- [ ] Build fails (if coverage < 75%) ? This is GOOD! Enforcement works!

---

## ?? **Phase 4: Commit Changes** (3 minutes)

### **? Step 8: Stage Files**

```bash
git add Directory.Build.props
git add coverage.ps1
git add coverage.sh
git add COVERAGE.md
git add COVERAGE_QUICKSTART.md
git add COVERLET_IMPLEMENTATION_COMPLETE.md
git add Infrastructure.Tests/Infrastructure.Tests.csproj
git add Presentation.Tests/Presentation.Tests.csproj
git add .gitignore
```

### **? Step 9: Commit**

```bash
git commit -m "feat: add Coverlet code coverage with ReportGenerator

- Add coverlet.msbuild to test projects
- Create centralized coverage config (Directory.Build.props)
- Add PowerShell and Bash scripts for local coverage
- Set initial threshold to [YOUR_BASELINE + 10]%
- Exclude DTOs, migrations, entities from coverage
- Add comprehensive documentation (COVERAGE.md)
- Create GitHub Actions workflow template (not active yet)"
```

### **? Step 10: Push**

```bash
git push origin features/coverlet-and-playwrite-tests
```

- [ ] Push succeeds
- [ ] No merge conflicts

---

## ?? **Phase 5: CI Enforcement** (Optional - Recommended!)

### **? Step 11: Enable GitHub Actions**

**Only do this if you're ready for CI enforcement!**

```bash
# This activates coverage enforcement in CI
git add .github/workflows/test-coverage.yml
git commit -m "ci: enable code coverage enforcement in GitHub Actions

- Enforce 80% line coverage threshold
- Generate HTML reports on every push/PR
- Upload coverage artifacts
- Block builds below threshold"

git push
```

### **? Step 12: Verify CI**

1. **Go to:** GitHub repository ? Actions tab
2. **Check:** Workflow appears and runs
3. **Verify:** 
   - [ ] Build succeeds (if coverage ? 80%)
   - [ ] Build fails (if coverage < 80%)
   - [ ] Coverage report uploaded as artifact

**If build fails:**
- ? Expected! You need to add more tests
- ?? Lower threshold temporarily in `.github/workflows/test-coverage.yml`
- ?? Gradually increase as you add tests

---

## ?? **Phase 6: Gradual Improvement** (Ongoing)

### **? Step 13: Weekly Goals**

**Week 1:**
- [ ] Baseline established: ____%
- [ ] Threshold set to: baseline + 10%
- [ ] All builds passing

**Week 2:**
- [ ] Add tests for critical uncovered code
- [ ] Increase threshold by 5-10%
- [ ] Target: 70-75% coverage

**Week 3:**
- [ ] Continue adding tests
- [ ] Increase threshold to 75-80%
- [ ] Enable CI enforcement (if not done yet)

**Week 4:**
- [ ] Reach 80% target
- [ ] Refine exclusions (if needed)
- [ ] Document coverage requirements for team

---

## ?? **Success Criteria**

You're done when:

- ? Script runs successfully: `./coverage.ps1`
- ? HTML report opens: `coverage-report/index.html`
- ? Baseline recorded: ____%
- ? Threshold configured: baseline + 10%
- ? Threshold enforcement tested and working
- ? Changes committed and pushed
- ? (Optional) CI enabled and passing

---

## ?? **Common Issues**

### **Script won't run (Windows)**
```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
./coverage.ps1
```

### **Script won't run (Linux/Mac)**
```bash
chmod +x coverage.sh
./coverage.sh
```

### **No coverage files found**
```bash
# Restore packages
dotnet restore

# Try explicit command
dotnet test /p:CollectCoverage=true
```

### **ReportGenerator not found**
```bash
dotnet tool install --global dotnet-reportgenerator-globaltool
```

### **Threshold too aggressive (build always fails)**
- Lower threshold in `Directory.Build.props`
- Example: From 80% to 60%
- Gradually increase as you add tests

---

## ?? **Need Help?**

1. **Read:** `COVERAGE.md` - Comprehensive guide
2. **Read:** `COVERAGE_QUICKSTART.md` - Quick reference
3. **Check:** HTML report to see what's uncovered
4. **Run:** `dotnet test --verbosity detailed` for detailed logs

---

## ? **Quick Reference**

| Command | Purpose |
|---------|---------|
| `./coverage.ps1` | Generate coverage report (Windows) |
| `./coverage.sh` | Generate coverage report (Linux/Mac) |
| `./coverage.ps1 80` | Enforce 80% threshold |
| `dotnet test /p:CollectCoverage=true` | Collect coverage (no report) |

---

## ?? **You're Ready!**

Check off each item above, and you'll have a fully functional code coverage system!

**Current Progress:**
- ? Implementation complete
- ? Baseline check pending
- ? Threshold configuration pending
- ? CI enforcement optional

**Next Action:** Run `./coverage.ps1` and note your baseline! ??

---

**Happy Testing!** ??
