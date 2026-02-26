# CHANGELOG

## 2026-02-27

### Added
1. Enforced push workflow note: update modification records before every push.

### Changed
1. Refactored dynamic form item access in [`Views/DynamicEntity/_Form.cshtml`](/Users/tt/Desktop/ws/ccc/DynamicCrudSample/Views/DynamicEntity/_Form.cshtml).
2. Replaced repeated runtime cast/try-catch field reads with safe dictionary access (`TryGetValue`).
3. Removed nullable-related build warnings from form rendering path.

### Verification
1. `dotnet build` succeeded with `0 warning / 0 error`.
