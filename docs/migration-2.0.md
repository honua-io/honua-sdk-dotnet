# Planned 2.0 API cleanup

This page records compatibility debt that remains public during the stable 1.x
line. It is a planning document, not permission to remove these APIs from a 1.x
release. Every breaking change still requires the major-release compatibility
workflow and release notes.

## Expression evaluator exception

`Honua.Sdk.Field.Forms.Expressions.ExpressionException` was exposed even though
expression failures are returned as `ExpressionResult` diagnostics. The 1.x
assembly keeps the public type for binary compatibility and hides it from
IntelliSense, while the evaluator now uses an internal control-flow exception.
In 2.0, remove or internalize the legacy public exception. Consumers should use
`ExpressionEvaluator.EvaluateDetailed` and inspect `ExpressionResult` rather
than catching that exception.

## SDK transport plumbing

The following `Honua.Sdk.Abstractions` types remain public and callable in 1.x
but are hidden from IntelliSense because they exist to share implementation
across SDK packages:

- `HonuaClientOptionsValidation`
- `HonuaResilienceTimeouts`
- `HonuaHttpHandlerDefaults`
- `Authentication.HonuaAuthenticationSupport`
- `Http.NextLinkOriginValidator`
- `Http.NonDisposingStream`
- `Http.ResponseOwningStream`

For 2.0, move this plumbing behind internal package boundaries or replace it
with a deliberately supported extension surface before removal. Applications
should configure clients through the `AddHonua*` registration methods and use
the typed client contracts instead of depending on these helpers directly.

## Replica sync errors

`ReplicaSyncException` is an intentionally public runtime failure, but changing
its base class is reserved for the next major version. It continues to derive
directly from `Exception` throughout 1.x to preserve the released binary type
hierarchy. In 2.0, derive it from `HonuaException`, map its existing `StatusCode`
value to the normalized `HttpStatus` override, and retain `ServerErrorCode` for
GeoServices error-envelope details.
