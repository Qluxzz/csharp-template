# Records and their footguns

## Equality

Records are comparable as long as all the fields within a record are equatable, meaning the type implement IEquatable, here's the list of built in types that does that https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1#derived:

```csharp
record Test(string Name, int Age);

var me = new Test("André", 30);
var myClone = new Test("André", 30);

me == myClone // true
```

When if your record contains something that does not implement IEquality, like a list of strings, you can't use == to compare the instances anymore.

```csharp
record Test(string Name, int Age, List<string> Hobbies);

var me = new Test("André", 30, ["Bouldering", "Reading"]);
var myClone = new Test("André", 30, ["Boludering", "Reading"]);

me == myClone // false
```

This is because a list is a reference type and we're creating a new list for each instance.

Just to drive this home, here is an example where we create the list once, and reuse it for both instances. Now the records are equal again.

```csharp
record Test(string Name, int Age, List<string> Hobbies);

var hobbies = new List<string>() { "Bouldering", "Reading" };

var me = new Test("André", 30, hobbies);
var myClone = new Test("André", 30, hobbies);

me == myClone // true
```

If you want to learn more I suggest reading this about value and reference equality: https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/statements-expressions-operators/equality-comparisons
