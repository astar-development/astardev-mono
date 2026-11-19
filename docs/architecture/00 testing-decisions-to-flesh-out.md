# testing

I think that's an excellent idea and, frankly, I would treat it as part of the architecture rather than part of testing.

There's a subtle distinction:

Plain Text
Unit Tests
verify behaviour
 
Architecture Tests
verify structure

The latter are closer to build-time governance.

For AStarDev.CloudSync, I'd absolutely want automated enforcement of rules like:

Plain Text
Domain
depends on nothing
Plain Text
UI
must not depend on Persistence
Plain Text
OneDrive
must not depend on Persistence
Plain Text
Persistence
must not depend on SyncEngine
 
Plain Text
SyncEngine
must not reference Graph SDK

Those aren't business tests. They're architectural invariants.

Personally I'd lean toward NetArchTest.Rules initially because it's lightweight and easy to express architectural fitness functions. If later you want stricter architectural modelling, ArchUnitNET offers more power but also more ceremony. For a solo project, I'd probably start with:

Plain Text
AStarDev.CloudSync.ArchitectureTests

and enforce the dependency rules from Document 11 directly in CI.

One of my favourite architecture tests is simply:

C#
Types
.InAssembly(domainAssembly)
.ShouldNot()
.HaveDependencyOnAny(
"Microsoft.EntityFrameworkCore",
"Microsoft.Graph",
"Avalonia")

because it catches erosion immediately.
