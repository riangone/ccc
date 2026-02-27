# CHANGELOG

## 2026-02-27

### Added
1. Added detailed Japanese file-level comments across custom `.cs` and `.cshtml` files.
2. Added detailed Japanese method-level comments in core backend files (`DynamicCrudRepository`, `DynamicEntityController`, `UserAuthService`).

### Fixed
1. Fixed Razor build issue caused by BOM before `@model` in `Views/Shared/Error.cshtml`.

### Added
1. Optional `count=false` query mode for large datasets to skip `COUNT(*)`.
2. Keyset pagination support via cursor for high-volume list views.

### Added
1. Enforced push workflow note: update modification records before every push.

### Added
1. Added an `isPublic` flag on entity metadata so each YAML definition can opt into appearing in the authenticated sidebar menu.

### Changed
1. Refactored dynamic form item access in [`Views/DynamicEntity/_Form.cshtml`](/Users/tt/Desktop/ws/ccc/DynamicCrudSample/Views/DynamicEntity/_Form.cshtml).
2. Replaced repeated runtime cast/try-catch field reads with safe dictionary access (`TryGetValue`).
3. Removed nullable-related build warnings from form rendering path.
4. Sidebar navigation now waits for login, iterates every YAML definition, and shows a `Public`/`Private` badge plus active-state styling based on the new `isPublic` metadata.
5. `Views/DynamicEntity/Index.cshtml` now renders breadcrumb navigation to keep the current entity context explicit.

### Verification
1. `dotnet build` succeeded with `0 warning / 0 error`.
