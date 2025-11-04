# ?? Quick Start: Run Coverage Locally

## **Step 1: Run Coverage Script**

### Windows (PowerShell)
```powershell
./coverage.ps1
```

### Linux/Mac (Bash)
```bash
chmod +x coverage.sh  # One-time only
./coverage.sh
```

---

## **Step 2: View Report**

The script will automatically open `coverage-report/index.html` in your browser.

**If it doesn't open automatically:**
- Navigate to: `coverage-report/index.html`
- Open in any browser

---

## **Step 3: Check Your Coverage**

Look for the **summary table** at the top:

| Metric | Value | Target |
|--------|-------|--------|
| Line Coverage | **??%** | ? 80% |
| Branch Coverage | **??%** | - |
| Method Coverage | **??%** | - |

---

## **Step 4: Update Threshold**

1. **Note your current line coverage** (e.g., 65%)
2. **Open:** `Directory.Build.props`
3. **Update:**
   ```xml
   <Threshold>70</Threshold>  <!-- Current% + 5-10% -->
   ```
4. **Commit the change**

---

## **Step 5: Test Threshold Enforcement**

```powershell
# This should PASS if coverage ? 70%
./coverage.ps1 70
```

If it fails:
- ? Good! The enforcement is working
- ? Add more tests to increase coverage

---

## **Next: Add CI Enforcement**

See `COVERAGE.md` for GitHub Actions workflow template.

---

**That's it!** ??

You now have:
- ? Local coverage reporting
- ? HTML reports
- ? Threshold enforcement (optional)

**Time to write some tests!** ??
