# ?? Coverage Improvement Roadmap

## ?? **Current Status: 30.8% Line Coverage**

**Goal:** Reach 80% line coverage over 3 weeks

**Strategy:** Focus on business logic first, exclude DTOs/migrations

---

## ?? **Coverage Baseline (After Exclusions)**

After updating `Directory.Build.props` to exclude:
- ? Migrations (3,000+ lines of generated code)
- ? DTOs/Models (no business logic)
- ? Entities (POCOs)

**Run this to see real baseline:**
```powershell
.\coverage.ps1
```

**Expected:** Coverage will jump to ~45-50% after exclusions

---

## ?? **High CRAP Score Targets (Priority 1)**

These methods have **CRAP scores > 30** and **NO tests**:

### **1. MatchesController.DeclineMatch() - CRAP: 156**
**Why High CRAP:**
- Complexity: 12
- Coverage: 0%
- Has validation logic that's untested

**What to Test:**
```csharp
// Presentation.Tests/Controllers/MatchesControllerDeclineTests.cs
- Valid decline request ? 204 NoContent
- Unauthorized user ? 401
- Invalid targetUserId (null, empty, whitespace) ? 400
- Invalid GUID format ? 400
- Invalid TmdbId (0, negative) ? 400
- Service throws exception ? 500
```

**Current Test Coverage:** `Presentation.Tests/Controllers/MatchesControllerTests.cs` exists but doesn't test `DeclineMatch()`

---

### **2. MyInformationController.GetMyInformation() - CRAP: 156**
**Why High CRAP:**
- Complexity: 12
- Coverage: 0%
- **Entire controller is untested**

**What to Test:**
```csharp
// Presentation.Tests/Controllers/MyInformationControllerTests.cs (NEW FILE)
- Valid request ? 200 OK with user data
- Unauthorized user ? 401
- User not found ? 404
- Service throws exception ? 500
```

**File Needed:** `Presentation.Tests/Controllers/MyInformationControllerTests.cs`

---

### **3. ChatsController.GetRoomMetadata() - CRAP: 110**
**Why High CRAP:**
- Complexity: 10
- Coverage: 0%
- Complex room validation logic

**What to Test:**
```csharp
// Add to: Presentation.Tests/Controllers/ChatsControllerTests.cs
- Valid room metadata request ? 200 OK
- Room doesn't exist ? 404
- User not member of room ? 403
- Invalid roomId format ? 400
```

**Current Test Coverage:** 46.8% (some methods tested, not this one)

---

### **4. MatchesController.GetMatchStatus() - CRAP: 110**
**Why High CRAP:**
- Complexity: 10
- Coverage: 0%
- Complex status calculation logic

**What to Test:**
```csharp
// Add to: Presentation.Tests/Controllers/MatchesControllerTests.cs
- Status: none ? 200 OK
- Status: pending_sent ? 200 OK
- Status: pending_received ? 200 OK
- Status: matched ? 200 OK with roomId
- Invalid targetUserId ? 400
- Self-check ? 400
```

---

### **5. TmdbClient.DiscoverAsync() - CRAP: 22**
**Why CRAP (but different):**
- Complexity: 22 (high complexity)
- Coverage: **Already tested!**
- CRAP score is due to complexity, NOT lack of tests

**Action:** ? **No action needed** - this is acceptable CRAP for external API client

---

## ?? **Phase 1: Controller Tests** (Week 1 - Target: 50% coverage)

### **Priority Tests to Add:**

| Controller | Method | Current % | Target % | CRAP Score |
|------------|--------|-----------|----------|------------|
| MyInformationController | GetMyInformation() | 0% | 100% | 156 |
| MatchesController | DeclineMatch() | 0% | 100% | 156 |
| MatchesController | GetMatchStatus() | 0% | 100% | 110 |
| ChatsController | GetRoomMetadata() | 0% | 100% | 110 |
| MoviesController | Discover() | 60.4% | 85% | Low |
| PreferencesController | SavePreferences() | 62.9% | 85% | Low |

### **Files to Create/Update:**

```
Presentation.Tests/Controllers/
?? MyInformationControllerTests.cs         [NEW] ? Create this
?? MatchesControllerTests.cs    [EXISTS] ? Add DeclineMatch(), GetMatchStatus()
?? ChatsControllerTests.cs   [EXISTS] ? Add GetRoomMetadata()
?? MoviesControllerTests.cs     [EXISTS] ? Add edge cases
?? PreferencesControllerTests.cs       [EXISTS] ? Add validation tests
```

---

## ?? **Phase 2: Service Tests** (Week 2 - Target: 65% coverage)

### **Services with Room for Improvement:**

| Service | Current % | Target % | Notes |
|---------|-----------|----------|-------|
| PreferenceService | 69.4% | 85% | Missing edge case validation |
| UserLikesService | 82% | 90% | Almost there! Add error handling tests |
| MatchService | 97.2% | 98% | ? Excellent already! |
| ChatService | 100% | 100% | ? Perfect! |

### **What to Add:**

**PreferenceService:**
```csharp
// Infrastructure.Tests/Services/PreferenceServiceTests.cs
- Concurrent updates (race conditions)
- Database connection failures
- Invalid genre IDs
- Transaction rollback scenarios
```

**UserLikesService:**
```csharp
// Infrastructure.Tests/Services/UserLikesServiceTests.cs
- Duplicate likes (idempotency)
- Concurrent like operations
- Database constraint violations
- Movie metadata validation
```

---

## ?? **Phase 3: Hub/SignalR Tests** (Week 3 - Target: 80% coverage)

### **ChatHub - Current: 76.3%**

**What's Missing:**
```csharp
// Presentation.Tests/Hubs/ChatHubTests.cs
- Connection/disconnection edge cases
- Multiple simultaneous connections from same user
- Message ordering under load
- Error recovery scenarios
- Group management edge cases
```

---

## ?? **Weekly Milestones:**

### **Week 1: Foundation**
- [ ] Update `Directory.Build.props` (exclude DTOs/migrations)
- [ ] Run coverage: `.\coverage.ps1`
- [ ] Baseline after exclusions: ____%
- [ ] Create `MyInformationControllerTests.cs`
- [ ] Add `DeclineMatch()` tests to `MatchesControllerTests.cs`
- [ ] Add `GetMatchStatus()` tests to `MatchesControllerTests.cs`
- [ ] Add `GetRoomMetadata()` tests to `ChatsControllerTests.cs`
- [ ] **Target:** 50% coverage

### **Week 2: Depth**
- [ ] Add edge case tests to `PreferenceService`
- [ ] Add error handling tests to `UserLikesService`
- [ ] Add validation tests to `MoviesController`
- [ ] Add concurrency tests where applicable
- [ ] **Target:** 65% coverage

### **Week 3: Polish**
- [ ] Add SignalR edge cases to `ChatHubTests`
- [ ] Add integration tests for complex flows
- [ ] Review CRAP scores - all should be < 30
- [ ] Enable CI enforcement (push workflow)
- [ ] **Target:** 80% coverage

---

## ??? **How to Add Tests:**

### **Example: MyInformationController**

1. **Create the test file:**
```csharp
// Presentation.Tests/Controllers/MyInformationControllerTests.cs
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Security.Claims;
using Xunit;

namespace Presentation.Tests.Controllers
{
    public sealed class MyInformationControllerTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

      public MyInformationControllerTests(WebApplicationFactory<Program> factory)
 {
          _factory = factory;
        }

        [Fact]
    public async Task GetMyInformation_Authenticated_ReturnsUserData()
        {
   // Arrange
        var client = _factory.CreateClient();
            // TODO: Add auth token

            // Act
    var response = await client.GetAsync("/api/myinformation");

      // Assert
     response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

    [Fact]
        public async Task GetMyInformation_Unauthenticated_Returns401()
 {
            // Arrange
    var client = _factory.CreateClient();

          // Act
      var response = await client.GetAsync("/api/myinformation");

            // Assert
 response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
```

2. **Run tests:**
```powershell
dotnet test
```

3. **Check coverage:**
```powershell
.\coverage.ps1
```

4. **Verify CRAP score dropped:**
   - Open `coverage-report/index.html`
   - Check "Risk Hotspots" section
   - Verify `MyInformationController.GetMyInformation()` is no longer red

---

## ?? **Tracking Progress:**

### **Coverage Trend:**

| Date | Line % | Branch % | Notes |
|------|--------|----------|-------|
| 2025-11-04 | 30.8% | 67.8% | Initial baseline (before exclusions) |
| 2025-11-__ | __% | __% | After excluding DTOs/migrations |
| 2025-11-__ | __% | __% | After controller tests (Week 1) |
| 2025-11-__ | __% | __% | After service tests (Week 2) |
| 2025-11-__ | __% | __% | After hub tests (Week 3) |

### **CRAP Score Trend:**

| Method | Week 0 | Week 1 | Week 2 | Week 3 |
|--------|--------|--------|--------|--------|
| DeclineMatch() | 156 | __ | __ | < 30 ? |
| GetMyInformation() | 156 | __ | __ | < 30 ? |
| GetRoomMetadata() | 110 | __ | __ | < 30 ? |
| GetMatchStatus() | 110 | __ | __ | < 30 ? |

---

## ?? **Success Criteria:**

### **Week 1: ? Foundation Complete**
- [ ] Coverage ? 50%
- [ ] All CRAP > 100 methods tested
- [ ] `MyInformationController` has tests

### **Week 2: ? Depth Added**
- [ ] Coverage ? 65%
- [ ] All CRAP > 30 methods tested
- [ ] Service edge cases covered

### **Week 3: ? Target Reached**
- [ ] Coverage ? 80%
- [ ] All CRAP scores < 30
- [ ] CI enforcement enabled
- [ ] Documentation updated

---

## ?? **Next Steps (Today):**

1. **Update exclusions:**
 ```powershell
   # Already done! Directory.Build.props updated
   ```

2. **Re-run coverage:**
   ```powershell
.\coverage.ps1
 ```

3. **Note new baseline:**
   ```
   After exclusions: ____%
   ```

4. **Update threshold in Directory.Build.props:**
   ```xml
   <Threshold>35</Threshold>  <!-- Set to baseline + 5% -->
   ```

5. **Commit changes:**
   ```bash
   git add Directory.Build.props coverage.ps1 COVERAGE_IMPROVEMENT_ROADMAP.md
git commit -m "chore: update coverage config and add improvement roadmap"
   git push
   ```

6. **Start Week 1 tasks** (see above)

---

## ?? **Resources:**

- **CRAP Score Explained:** https://testing.googleblog.com/2011/02/this-code-is-crap.html
- **xUnit Docs:** https://xunit.net/
- **FluentAssertions:** https://fluentassertions.com/
- **WebApplicationFactory:** https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests

---

## ?? **Pro Tips:**

### **1. Test Business Logic, Not Boilerplate**
? **DO Test:**
- Services (business logic)
- Controllers (validation, authorization)
- Hubs (connection management)

? **DON'T Test:**
- DTOs (just properties)
- Entities (just properties)
- Migrations (generated code)

### **2. Focus on High CRAP Scores First**
- CRAP > 100: ?? Urgent
- CRAP 50-100: ?? High priority
- CRAP 30-50: ?? Medium priority
- CRAP < 30: ? Acceptable

### **3. Use Coverage to Find Gaps, Not as the Goal**
- 80% coverage doesn't mean bug-free
- 100% coverage is usually overkill
- Focus on **critical paths** and **edge cases**

---

**Last Updated:** November 4, 2025  
**Current Coverage:** 30.8% ? Target: 80%  
**Status:** ?? In Progress (Week 0 - Baseline)  
**Next Milestone:** Week 1 - 50% coverage
