using System.Runtime.CompilerServices;

// Pure rules (attendance badges, analytics deltas) are unit-tested without standing up services.
[assembly: InternalsVisibleTo("Vispl.HRManagement.Tests")]
