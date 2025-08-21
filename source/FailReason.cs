namespace Allocator;

enum AllocFailReason { None, SizeNegative, Overflow };
enum ReleaseFailReason { None, Null, AlreadyReleased };