# NextUnit Best Practices Guide

This guide provides proven patterns and recommendations for writing effective tests with NextUnit.

## Table of Contents

1. [Test Naming](#test-naming)
2. [Test Organization](#test-organization)
3. [Assertions](#assertions)
4. [Lifecycle Management](#lifecycle-management)
5. [Parallel Execution](#parallel-execution)
6. [Test Data](#test-data)
7. [Common Patterns](#common-patterns)
8. [Performance](#performance)
9. [Troubleshooting](#troubleshooting)

## Test Naming

### Use Descriptive Names

Follow the pattern: `MethodName_Scenario_ExpectedResult`

✅ **Good**:

```csharp
[Test]
public void Add_TwoPositiveNumbers_ReturnsCorrectSum()
{
    var result = Calculator.Add(2, 3);
    Assert.Equal(5, result);
}

[Test]
public void Divide_ByZero_ThrowsDivideByZeroException()
{
    Assert.Throws<DivideByZeroException>(() => Calculator.Divide(10, 0));
}
```

❌ **Avoid**:

```csharp
[Test]
public void Test1() { } // What does this test?

[Test]
public void AddTest() { } // Which scenario?
```

### Be Specific About Scenarios

```csharp
✅ public void Process_EmptyList_ReturnsEmptyResult()
✅ public void Process_NullInput_ThrowsArgumentNullException()
✅ public void Process_ValidData_UpdatesDatabase()

❌ public void Process_Works()
❌ public void TestProcessing()
```

## Test Organization

### Group Related Tests in Classes

```csharp
// ✅ Organize by feature/component
public class UserServiceTests
{
    [Test]
    public void CreateUser_ValidData_ReturnsNewUser() { }

    [Test]
    public void CreateUser_DuplicateEmail_ThrowsException() { }

    [Test]
    public void DeleteUser_ExistingUser_RemovesFromDatabase() { }
}

// ✅ Separate integration and unit tests
public class UserServiceUnitTests { }
public class UserServiceIntegrationTests { }
```

### Use Nested Classes for Logical Grouping

```csharp
public class CalculatorTests
{
    public class AdditionTests
    {
        [Test]
        public void Add_PositiveNumbers_ReturnsSum() { }

        [Test]
        public void Add_NegativeNumbers_ReturnsSum() { }
    }

    public class DivisionTests
    {
        [Test]
        public void Divide_ByZero_ThrowsException() { }

        [Test]
        public void Divide_ValidNumbers_ReturnsQuotient() { }
    }
}
```

## Assertions

### One Logical Assertion Per Test

✅ **Good** - Single focus:

```csharp
[Test]
public void CreateUser_ValidData_SetsCorrectName()
{
    var user = new User("John", "Doe");
    Assert.Equal("John", user.FirstName);
}

[Test]
public void CreateUser_ValidData_SetsCorrectLastName()
{
    var user = new User("John", "Doe");
    Assert.Equal("Doe", user.LastName);
}
```

⚠️ **Acceptable** - Related assertions:

```csharp
[Test]
public void CreateUser_ValidData_SetsAllProperties()
{
    var user = new User("John", "Doe", 30);
    
    Assert.Equal("John", user.FirstName);
    Assert.Equal("Doe", user.LastName);
    Assert.Equal(30, user.Age);
}
```

### Use the Right Assertion

```csharp
// ✅ Specific assertions provide better error messages
Assert.Equal(expected, actual);
Assert.Contains(item, collection);
Assert.StartsWith(prefix, text);

// ❌ Avoid generic assertions
Assert.True(actual == expected); // Less informative error
Assert.True(collection.Contains(item)); // Use Assert.Contains
```

### Assert on Meaningful Values

```csharp
// ✅ Test business logic
Assert.Equal(250, order.TotalPrice);
Assert.Contains("Premium", user.Roles);

// ❌ Don't test framework behavior
Assert.NotNull(list); // Lists are never null in C#
Assert.True(true); // Meaningless
```

## Lifecycle Management

### Choose the Right Scope

```csharp
// Test scope - Clean state for each test (default)
public class IsolatedTests
{
    private Database? _db;

    [Before(LifecycleScope.Test)]
    public void Setup()
    {
        _db = new Database(); // Fresh instance per test
    }

    [After(LifecycleScope.Test)]
    public void Cleanup()
    {
        _db?.Dispose();
    }

    [Test]
    public void Test1() { /* _db is clean */ }

    [Test]
    public void Test2() { /* _db is clean */ }
}

// Class scope - Share expensive resources
public class SharedResourceTests
{
    private static ExpensiveResource? _resource;

    [Before(LifecycleScope.Class)]
    public void ClassSetup()
    {
        _resource = new ExpensiveResource(); // Once per class
    }

    [After(LifecycleScope.Class)]
    public void ClassTeardown()
    {
        _resource?.Dispose();
    }

    [Test]
    public void Test1() { /* Shares _resource */ }

    [Test]
    public void Test2() { /* Shares _resource */ }
}
```

### Prefer Test Scope for Isolation

```csharp
// ✅ Good - Tests are independent
[Before(LifecycleScope.Test)]
public void Setup()
{
    _list = new List<int>();
}

// ⚠️ Risky - Tests might interfere
[Before(LifecycleScope.Class)]
public void ClassSetup()
{
    _list = new List<int>(); // Shared between tests!
}
```

### Use Assembly Scope Sparingly

```csharp
// ✅ Assembly scope for true global setup
public class DatabaseTests
{
    [Before(LifecycleScope.Assembly)]
    public void InitializeDatabase()
    {
        // Migrate database schema once
        DatabaseMigrator.Migrate();
    }

    [After(LifecycleScope.Assembly)]
    public void CleanupDatabase()
    {
        // Drop test database
        DatabaseMigrator.Drop();
    }
}
```

## Parallel Execution

### Embrace Parallelism by Default

```csharp
// ✅ Default - Tests run in parallel
public class FastTests
{
    [Test]
    public void Test1() { } // Runs in parallel

    [Test]
    public void Test2() { } // Runs in parallel
}
```

### Use [NotInParallel] for State-Dependent Tests

```csharp
// ✅ Prevent parallel execution when tests share state
[NotInParallel]
public class FileSystemTests
{
    [Test]
    public void CreateFile_CreatesFileOnDisk()
    {
        File.WriteAllText("test.txt", "content");
        Assert.True(File.Exists("test.txt"));
    }

    [Test]
    public void DeleteFile_RemovesFileFromDisk()
    {
        File.Delete("test.txt");
        Assert.False(File.Exists("test.txt"));
    }
}
```

### Use [ParallelLimit] for Resource Constraints

```csharp
// ✅ Limit parallelism for resource-intensive tests
[ParallelLimit(2)]
public class DatabaseIntegrationTests
{
    [Test]
    public void Test1() { /* Uses database connection */ }

    [Test]
    public void Test2() { /* Uses database connection */ }

    [Test]
    public void Test3() { /* Uses database connection */ }
}
```

## Test Data

### Use [Arguments] for Simple Cases

```csharp
// ✅ Inline data for simple parameterization
[Test]
[Arguments(1, 2, 3)]
[Arguments(5, 5, 10)]
[Arguments(0, 0, 0)]
[Arguments(-1, 1, 0)]
public void Add_ReturnsCorrectSum(int a, int b, int expected)
{
    var result = a + b;
    Assert.Equal(expected, result);
}
```

### Use Helper Methods for Complex Data

```csharp
public class EmailValidationTests
{
    private static readonly string[] ValidEmails = new[]
    {
        "user@example.com",
        "test.user@example.co.uk",
        "user+tag@example.com"
    };

    [Test]
    public void Validate_ValidEmails_ReturnsTrue()
    {
        foreach (var email in ValidEmails)
        {
            Assert.True(EmailValidator.IsValid(email), 
                $"Expected {email} to be valid");
        }
    }
}
```

## Common Patterns

### Test Exceptions Properly

```csharp
// ✅ Verify exception type and optionally message
[Test]
public void Process_NullInput_ThrowsArgumentNullException()
{
    var ex = Assert.Throws<ArgumentNullException>(() => 
        service.Process(null));
    
    Assert.Contains("input", ex.Message);
}

// ✅ For async code
[Test]
public async Task ProcessAsync_NullInput_ThrowsArgumentNullException()
{
    var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => 
        service.ProcessAsync(null));
}
```

### Test Async Code

```csharp
// ✅ Use async/await naturally
[Test]
public async Task GetDataAsync_ValidId_ReturnsData()
{
    var data = await repository.GetDataAsync(123);
    
    Assert.NotNull(data);
    Assert.Equal(123, data.Id);
}

// ❌ Don't block on async code
[Test]
public void GetDataAsync_ValidId_ReturnsData_WRONG()
{
    var data = repository.GetDataAsync(123).Result; // Deadlock risk!
    Assert.NotNull(data);
}
```

### Test Collections

```csharp
// ✅ Use collection-specific assertions
[Test]
public void GetUsers_ReturnsExpectedUsers()
{
    var users = repository.GetUsers();
    
    Assert.NotEmpty(users);
    Assert.Contains(expectedUser, users);
    Assert.All(users, user => Assert.NotNull(user.Name));
}

// ✅ Test exact count when important
[Test]
public void GetActiveUsers_ReturnsTwoUsers()
{
    var users = repository.GetActiveUsers();
    
    Assert.Equal(2, users.Count);
}
```

### Use Dependencies Correctly

```csharp
// ✅ Explicit ordering for integration tests
public class CheckoutWorkflowTests
{
    [Test]
    public void Step1_CreateOrder()
    {
        // Create order
    }

    [Test]
    [DependsOn(nameof(Step1_CreateOrder))]
    public void Step2_ProcessPayment()
    {
        // Process payment for created order
    }

    [Test]
    [DependsOn(nameof(Step2_ProcessPayment))]
    public void Step3_ShipOrder()
    {
        // Ship the paid order
    }
}
```

## Performance

### Minimize Setup/Teardown Work

```csharp
// ✅ Use class scope for expensive setup
[Before(LifecycleScope.Class)]
public void ClassSetup()
{
    _heavyResource = new ExpensiveResource(); // Once
}

// ❌ Don't repeat expensive work
[Before(LifecycleScope.Test)]
public void TestSetup()
{
    _heavyResource = new ExpensiveResource(); // Every test!
}
```

### Keep Tests Fast

```csharp
// ✅ Mock external dependencies
[Test]
public void SendEmail_ValidRecipient_CallsEmailService()
{
    var mockEmailService = new MockEmailService();
    var notifier = new Notifier(mockEmailService);
    
    notifier.SendEmail("user@example.com", "Test");
    
    Assert.Equal(1, mockEmailService.CallCount);
}

// ❌ Don't call real external services in unit tests
[Test]
public void SendEmail_ValidRecipient_SendsRealEmail()
{
    var notifier = new Notifier(new RealEmailService());
    notifier.SendEmail("user@example.com", "Test"); // Slow!
}
```

### Use Parallel Execution

```csharp
// ✅ Default parallelism for fast test suites
public class UnitTests
{
    [Test] public void Test1() { } // Parallel
    [Test] public void Test2() { } // Parallel
    [Test] public void Test3() { } // Parallel
}

// Result: All tests run simultaneously on available CPU cores
```

## Troubleshooting

### Tests Pass Alone But Fail Together

**Cause**: Shared state between tests

**Solution**: Use `[NotInParallel]` or fix state isolation

```csharp
// ✅ Fix: Isolate state
[NotInParallel]
public class StatefulTests
{
    private static int _counter = 0;

    [Before(LifecycleScope.Test)]
    public void Reset()
    {
        _counter = 0; // Reset between tests
    }
}

// ✅ Better: Avoid shared state
public class BetterTests
{
    [Test]
    public void Test1()
    {
        int counter = 0; // Local state
        counter++;
        Assert.Equal(1, counter);
    }
}
```

### Flaky Tests

**Cause**: Timing issues, randomness, external dependencies

**Solution**: Make tests deterministic

```csharp
// ❌ Flaky: Depends on timing
[Test]
public void ProcessAsync_CompletesQuickly()
{
    var task = service.ProcessAsync();
    Task.Delay(100).Wait();
    Assert.True(task.IsCompleted); // Might fail!
}

// ✅ Deterministic: Await completion
[Test]
public async Task ProcessAsync_Completes()
{
    await service.ProcessAsync();
    Assert.True(true); // Always passes
}
```

### Slow Test Execution

**Cause**: Sequential execution, expensive setup

**Solutions**:

1. Remove `[NotInParallel]` if not needed
2. Use class/assembly scope for setup
3. Mock expensive dependencies
4. Use `[ParallelLimit]` appropriately

```csharp
// ✅ Parallel execution (default)
public class FastTests { }

// ✅ Shared setup
[Before(LifecycleScope.Class)]
public void ClassSetup() { }

// ✅ Controlled parallelism
[ParallelLimit(4)]
public class ModerateTests { }
```

## Summary

### Golden Rules

1. **Name tests clearly** - `MethodName_Scenario_ExpectedResult`
2. **One assertion per test** (or closely related assertions)
3. **Isolate test state** - Prefer `Test` scope
4. **Embrace parallelism** - Use `[NotInParallel]` only when needed
5. **Keep tests fast** - Mock dependencies, minimize I/O
6. **Make tests deterministic** - No timing dependencies
7. **Use the right assertion** - Specific > Generic
8. **Document complex tests** - Comments for unusual patterns

### Quick Checklist

- [ ] Test names describe scenario and expected result
- [ ] Tests are independent (can run in any order)
- [ ] Assertions use specific methods (`Equal`, `Contains`, etc.)
- [ ] Async tests use `async`/`await` properly
- [ ] Expensive setup uses class/assembly scope
- [ ] External dependencies are mocked
- [ ] Tests run in parallel (unless state-dependent)
- [ ] No `Thread.Sleep` or timing dependencies

## Further Reading

- [Getting Started Guide](GETTING_STARTED.md) - Basic usage
- [Migration Guide](MIGRATION_FROM_XUNIT.md) - Coming from xUnit
- [Performance Analysis](PERFORMANCE.md) - Benchmarks and optimization tips
- [CI/CD Integration](CI_CD_INTEGRATION.md) - Continuous integration setup

---

**Remember**: Good tests are fast, isolated, deterministic, and easy to understand.
NextUnit's parallel execution and multi-scope lifecycle make it easy to write efficient,
maintainable test suites!
