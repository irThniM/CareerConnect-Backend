# API route scaffolds

The remaining controller classes reserve module names and route prefixes for later implementation. `ApplicationsController` is implemented and uses role and company-membership checks. Add authorization checks before exposing actions from other modules.

Do not add the new entities to `AppDbContext` or create a migration until the current Neon schema differences are reviewed and each relationship is configured.
