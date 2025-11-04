# ?? Coverage Quick Reference

## Run Coverage Report
```powershell
.\coverage.ps1
```

## What Gets Excluded
- ? EF Core Migrations
- ? Designer files
- ? Model snapshots
- ? Auto-generated code

## Your Current Coverage
- **Overall:** 96.9% ?
- **Infrastructure:** 100% ?
- **Presentation:** 95.1% ?

## Manual Command (if needed)
```powershell
# Run tests with coverage
dotnet test --settings coverlet.runsettings --collect:"XPlat Code Coverage"

# Generate report (migrations excluded!)
reportgenerator `
  "-reports:**\TestResults\**\coverage.cobertura.xml" `
  "-targetdir:coverage-report" `
  "-reporttypes:Html" `
  "-classfilters:-Infrastructure.Migrations.*;-*.Designer;-*ModelSnapshot"

# Open report
Start-Process coverage-report\index.html
```

## Troubleshooting

**Migrations still showing?**
- Make sure you're using `.\coverage.ps1` script
- Check that class filters are applied in the script

**Old coverage files?**
```powershell
Remove-Item -Recurse -Force TestResults, coverage-report
```

**Need detailed output?**
```powershell
.\coverage.ps1 | Tee-Object coverage-log.txt
```

---

**? Your coverage is excellent! Keep it up!**
