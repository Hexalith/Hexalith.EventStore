## [2.1.1](https://github.com/Hexalith/Hexalith.EventStore/compare/v2.1.0...v2.1.1) (2026-04-30)


### Bug Fixes

* stabilize event replay and update retrospectives ([2532f39](https://github.com/Hexalith/Hexalith.EventStore/commit/2532f39e6e37ec3f0b0aa68f4645bfc87585b9ed))

# [2.1.0](https://github.com/Hexalith/Hexalith.EventStore/compare/v2.0.0...v2.1.0) (2026-04-29)


### Features

* Add Epic 6 retrospective documentation for observability and operations ([e690233](https://github.com/Hexalith/Hexalith.EventStore/commit/e690233e3d995d5b85f682d7a0db48864c244e54))

# [2.0.0](https://github.com/Hexalith/Hexalith.EventStore/compare/v1.4.0...v2.0.0) (2026-04-28)


* feat(client)!: throw MissingApplyMethodException on unknown event during rehydration ([2568f81](https://github.com/Hexalith/Hexalith.EventStore/commit/2568f81ea62273ff314b66bf76f06b909a7ecf2e))
* refactor(server)!: thread aggregate type through persistence pipeline ([2070c68](https://github.com/Hexalith/Hexalith.EventStore/commit/2070c6828f40a0cace523a48f106fac114d12c4d))


### Bug Fixes

* **contracts:** restore CommandEnvelope defensive copy for Extensions ([c79acec](https://github.com/Hexalith/Hexalith.EventStore/commit/c79acecf60cb74212b69fd6000788d679af601be))
* **ui:** apply Story 21-8 CSS review round 2 patches (monospace font-stack, link token) ([a634e93](https://github.com/Hexalith/Hexalith.EventStore/commit/a634e93216d670c44ca669ee4313e1b5d34da318))


### Features

* add perf lab + secret scanning + refresh NFR artifacts ([604beb5](https://github.com/Hexalith/Hexalith.EventStore/commit/604beb541e9bc90af09940445222e5296e106b72))
* **auth:** implement Dapr internal authentication handler and options with allow-list support ([3c24118](https://github.com/Hexalith/Hexalith.EventStore/commit/3c2411878178c8aa49dfe5410a0f8e1c23af5677))
* **contracts:** add CommandStatus.IsTerminal() extension and remove duplicate controller helpers ([1e4ea10](https://github.com/Hexalith/Hexalith.EventStore/commit/1e4ea10fbe8672e8fe07d7344cfa45cf7da9a2f0)), closes [#220](https://github.com/Hexalith/Hexalith.EventStore/issues/220)
* **testing:** add TerminatableComplianceAssertions helper (R1-A2) ([bdff4c4](https://github.com/Hexalith/Hexalith.EventStore/commit/bdff4c418d5fb57b574c3fddb41b508741ca008e))
* **ui:** fix NavMenu v5 styling and add Fluent UI CSS bundle (Story 21-11) ([3f2fc62](https://github.com/Hexalith/Hexalith.EventStore/commit/3f2fc6212f20a61cbaf3a74603414d007fddd46b))
* **ui:** fix theme toggle for Fluent UI v5 with data-theme CSS selectors (Story 21-12) ([cb6085b](https://github.com/Hexalith/Hexalith.EventStore/commit/cb6085bdc97b0c175507d82cff149f3521c38447))
* **ui:** fix UI bugs batch for Admin.UI v5 cleanup (Story 21-13) ([391c243](https://github.com/Hexalith/Hexalith.EventStore/commit/391c243dd0c3e062b15358643edeb7fb5a4e7722))
* **ui:** refactor App.razor and Routes.razor for improved error handling and routing structure ([986ae97](https://github.com/Hexalith/Hexalith.EventStore/commit/986ae9737dcb008fd06b9e672e190673b0de435c))


### BREAKING CHANGES

* Aggregate state rehydration now throws
MissingApplyMethodException for events lacking a matching public Apply method
on the state type, instead of silently skipping them. Consumers relying on the
prior skip behavior must declare an Apply method (no-op is sufficient,
particularly on ITerminatable states for AggregateTerminated).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
* IEventPersister.PersistEventsAsync signature changed;
all implementers and direct callers must pass `aggregateType` explicitly.

Closes story post-epic-1-r1a1-aggregatetype-pipeline.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>

# [1.4.0](https://github.com/Hexalith/Hexalith.EventStore/compare/v1.3.0...v1.4.0) (2026-04-15)


### Features

* **container:** migrate CD pipeline to .NET SDK container support ([#201](https://github.com/Hexalith/Hexalith.EventStore/issues/201)) ([3f8ad99](https://github.com/Hexalith/Hexalith.EventStore/commit/3f8ad9906fc26b07aa26563ecaab91c0627327b0))
* **ui:** fix Admin.UI.Tests runtime failures unmasked by v5 compile-green (Story 21-9.5.7) ([#206](https://github.com/Hexalith/Hexalith.EventStore/issues/206)) ([e962b2b](https://github.com/Hexalith/Hexalith.EventStore/commit/e962b2b0ee316112c42758bbafc64a5bebc01b81))
* **ui:** migrate Admin.UI CSS v4 FAST tokens to v5 Fluent 2 and merge scoped CSS (Story 21-8) ([#203](https://github.com/Hexalith/Hexalith.EventStore/issues/203)) ([20a4538](https://github.com/Hexalith/Hexalith.EventStore/commit/20a4538adf1c12af9da09b40290fa4aef6fac128))
* **ui:** migrate Admin.UI DataGrid enums and residual v5 renames (Story 21-9) ([#204](https://github.com/Hexalith/Hexalith.EventStore/issues/204)) ([400ecd1](https://github.com/Hexalith/Hexalith.EventStore/commit/400ecd1615c21350ec3d7f8f0d961af3e5c0411b))
* **ui:** migrate Admin.UI.Tests to Fluent UI Blazor v5 + bUnit v2 APIs (Story 21-9.5) ([#205](https://github.com/Hexalith/Hexalith.EventStore/issues/205)) ([6f1d210](https://github.com/Hexalith/Hexalith.EventStore/commit/6f1d2102633acd3101184fa597bc8af46ed209e9))
* **ui:** migrate FluentBadge and FluentAnchor to BadgeAppearance/LinkAppearance for Fluent UI Blazor v5 (Story 21-4) ([#197](https://github.com/Hexalith/Hexalith.EventStore/issues/197)) ([1d5c4bc](https://github.com/Hexalith/Hexalith.EventStore/commit/1d5c4bc10947f83e8360428f6997188fd031a423))
* **ui:** migrate FluentButton Appearance enum to ButtonAppearance for Fluent UI Blazor v5 (Story 21-3) ([4a17fc3](https://github.com/Hexalith/Hexalith.EventStore/commit/4a17fc34c9d132e9bc24f8feca8d0598a83eddb1))
* **ui:** migrate FluentTextField/NumberField/Search/ProgressRing/Select to Fluent UI Blazor v5 (Story 21-5) ([#198](https://github.com/Hexalith/Hexalith.EventStore/issues/198)) ([989f54e](https://github.com/Hexalith/Hexalith.EventStore/commit/989f54efacad7039738b7cc6a09d2ddb497c7168))
* **ui:** migrate IToastService.Show* calls to ShowToastAsync with extension helpers (Story 21-7) ([#202](https://github.com/Hexalith/Hexalith.EventStore/issues/202)) ([a544571](https://github.com/Hexalith/Hexalith.EventStore/commit/a54457141024f663f7ca779b2e8d3341ac89d119))
* **ui:** migrate layout, navigation, and theme to Fluent UI Blazor v5 (Story 21-2) ([515022e](https://github.com/Hexalith/Hexalith.EventStore/commit/515022e8449ec845c866752458a86a3c85a27417)), closes [#0066CC](https://github.com/Hexalith/Hexalith.EventStore/issues/0066CC)
* **ui:** migrate layout, navigation, and theme to Fluent UI Blazor v5 (Story 21-2) ([#195](https://github.com/Hexalith/Hexalith.EventStore/issues/195)) ([5ba1c8a](https://github.com/Hexalith/Hexalith.EventStore/commit/5ba1c8a8fc816564dfec45a39159b07f41d63cc0)), closes [#0066CC](https://github.com/Hexalith/Hexalith.EventStore/issues/0066CC)
* **ui:** migrate Sample.BlazorUI to Fluent UI Blazor v5 (Story 21-10) ([#207](https://github.com/Hexalith/Hexalith.EventStore/issues/207)) ([483149f](https://github.com/Hexalith/Hexalith.EventStore/commit/483149fc222b7123f1a6c61cc55749c6d24bbc6c))
* **ui:** restructure dialogs to Fluent UI Blazor v5 template slots (Story 21-6) ([#200](https://github.com/Hexalith/Hexalith.EventStore/issues/200)) ([ec37c58](https://github.com/Hexalith/Hexalith.EventStore/commit/ec37c58232888b16f0622da2933868af9002018c))
* **ui:** upgrade Fluent UI Blazor packages to v5 and update csproj infrastructure (Story 21-1) ([#194](https://github.com/Hexalith/Hexalith.EventStore/issues/194)) ([f79ec2e](https://github.com/Hexalith/Hexalith.EventStore/commit/f79ec2e55b7fa0c76b995fba7be526f57b652bef))

# [1.3.0](https://github.com/Hexalith/Hexalith.EventStore/compare/v1.2.6...v1.3.0) (2026-04-13)


### Features

* **server:** add stream activity tracker writer (Story 15-13) ([#192](https://github.com/Hexalith/Hexalith.EventStore/issues/192)) ([bce2614](https://github.com/Hexalith/Hexalith.EventStore/commit/bce2614f95b1d12c49a718282a07fb10b0a098d4))
* **server:** add stream activity tracker writer for admin UI data pipeline ([9b608db](https://github.com/Hexalith/Hexalith.EventStore/commit/9b608db8e7f9ced4dd113b63a4c4986c488b6f8c))

## [1.2.6](https://github.com/Hexalith/Hexalith.EventStore/compare/v1.2.5...v1.2.6) (2026-04-12)


### Bug Fixes

* **ci:** add missing Admin.Abstractions csproj to Dockerfile restore layer ([f149fe8](https://github.com/Hexalith/Hexalith.EventStore/commit/f149fe89b55b55234894c01a8a798ab1fc20ef25))

## [1.2.5](https://github.com/Hexalith/Hexalith.EventStore/compare/v1.2.4...v1.2.5) (2026-04-12)


### Bug Fixes

* **tests:** add missing messageId to integration test request payloads ([c465519](https://github.com/Hexalith/Hexalith.EventStore/commit/c465519020a215c3d5e1063c4a693563efb597de))

## [1.2.4](https://github.com/Hexalith/Hexalith.EventStore/compare/v1.2.3...v1.2.4) (2026-04-12)


### Bug Fixes

* **ci:** use dapr/setup-dapr action in release workflow and fix flaky UI test ([dd6445a](https://github.com/Hexalith/Hexalith.EventStore/commit/dd6445a0bb62474abe1a5e41dfbfc9a68a0153d2))

## [1.2.3](https://github.com/Hexalith/Hexalith.EventStore/compare/v1.2.2...v1.2.3) (2026-04-12)


### Bug Fixes

* **ci:** correct Dockerfile path in deploy-staging workflow ([1659566](https://github.com/Hexalith/Hexalith.EventStore/commit/1659566b2b376e59bb76ab88c107866d024766c0))
* **ci:** start Dapr infrastructure services after slim init ([0198206](https://github.com/Hexalith/Hexalith.EventStore/commit/0198206911b515f6ea0732f663db3ade42726643))
* **ci:** use dapr init --slim to avoid port conflicts on CI runners ([aba1f22](https://github.com/Hexalith/Hexalith.EventStore/commit/aba1f22af06b23d685ac44db49cbf5850f468d03))
* **ci:** use dapr/setup-dapr action in release workflow ([e546ce6](https://github.com/Hexalith/Hexalith.EventStore/commit/e546ce6813cea02c7ad4774d42666d2a72acfb3f))
* **ci:** use full dapr init to avoid scheduler port conflicts ([8c552f8](https://github.com/Hexalith/Hexalith.EventStore/commit/8c552f804dcb7f364d5486cc2773ba430649a525))

## [1.2.2](https://github.com/Hexalith/Hexalith.EventStore/compare/v1.2.1...v1.2.2) (2026-04-12)


### Bug Fixes

* **docs:** remove malformed closes reference in CHANGELOG.md ([f45239f](https://github.com/Hexalith/Hexalith.EventStore/commit/f45239fbafc64d0e0f2df17608eb7186ee64cf23))

## [1.2.1](https://github.com/Hexalith/Hexalith.EventStore/compare/v1.2.0...v1.2.1) (2026-04-12)


### Bug Fixes

* **release:** remove unsupported --verbosity flag from nuget push command ([9cf818c](https://github.com/Hexalith/Hexalith.EventStore/commit/9cf818c7b204af67e27cab08193f02c94e7e8177))

# [1.2.0](https://github.com/Hexalith/Hexalith.EventStore/compare/v1.1.1...v1.2.0) (2026-04-12)


### Bug Fixes

* **admin-ui:** bridge Keycloak JWT to Blazor authentication state ([abee3f3](https://github.com/Hexalith/Hexalith.EventStore/commit/abee3f3422ba98baed119d1bb6fd521fa448d00d))
* **admin-ui:** prevent FluentTabs overflow hiding Aggregates tab on Type Catalog page ([fc3df8d](https://github.com/Hexalith/Hexalith.EventStore/commit/fc3df8db4ebe79f98a36ff9829c1def31f5b6da0))
* **admin-ui:** show breadcrumb on Home page for consistent layout ([ec7bb67](https://github.com/Hexalith/Hexalith.EventStore/commit/ec7bb6796ad5f94be2b6e796eb6322ba54449d62))
* **admin:** improve post-mutation refresh, error feedback, and user-friendly error messages ([bf5f5c7](https://github.com/Hexalith/Hexalith.EventStore/commit/bf5f5c72c51718362cf615d6304eea88ab382211))
* **admin:** query pub/sub components from remote sidecar and fix dead-letter route ([a4bc7b5](https://github.com/Hexalith/Hexalith.EventStore/commit/a4bc7b56126212c02f760170de6216fb899e6c84))
* **admin:** read tenant projections from DAPR state store directly ([830973f](https://github.com/Hexalith/Hexalith.EventStore/commit/830973fe6880e3c2c01ca1bcad907dc703547fed))
* **admin:** replace binary remote-metadata flag with three-state diagnostic (story 19-6) ([e866755](https://github.com/Hexalith/Hexalith.EventStore/commit/e866755232534d699b6433510990c21e27ab9f15))
* **admin:** wire subscriptions counts and auto-inject resiliency config ([9b31601](https://github.com/Hexalith/Hexalith.EventStore/commit/9b31601bfb1a03048aa200de51f1218c1151390f))
* **auth:** apply code review round 2 patches for story 16-5 ([9c6a8ea](https://github.com/Hexalith/Hexalith.EventStore/commit/9c6a8ea9f9a1bf78ca1ac69dbb020cd5b030cc76))
* **auth:** bypass tenant and RBAC validation for global administrators ([a889380](https://github.com/Hexalith/Hexalith.EventStore/commit/a889380b9cdd1bb269077df8633134e55b3cca81))
* **auth:** skip API-level authorization for internal MediatR commands ([6a52678](https://github.com/Hexalith/Hexalith.EventStore/commit/6a52678b8de21329ac706008fe933a431880d4e6))
* **auth:** wire tenant bootstrap with deterministic Keycloak user IDs ([1468db5](https://github.com/Hexalith/Hexalith.EventStore/commit/1468db58539180ad2e3ce8b6aa36f78ac4c702ae))
* **build:** resolve Hexalith.Tenants paths dynamically for nested submodule support ([62f27e9](https://github.com/Hexalith/Hexalith.EventStore/commit/62f27e9bbe66f722910c950b6dc3092260682bd4))
* **ci:** checkout Hexalith.Tenants submodule in all workflows ([35101ff](https://github.com/Hexalith/Hexalith.EventStore/commit/35101ffa1ff9d568316e0a3904ffab5e237f4311))
* **docs:** repair broken links in docs-validation CI ([cbe9801](https://github.com/Hexalith/Hexalith.EventStore/commit/cbe98017d3c4189acba1501485a083374527ac43))
* move FluentDesignTheme to interactive circuit and fix status logic ([06b6831](https://github.com/Hexalith/Hexalith.EventStore/commit/06b6831a1f1bd880b30680bb2095923725319e0e))
* remove dead commandapi alias from PerConsumerRateLimitingTests ([11e62bd](https://github.com/Hexalith/Hexalith.EventStore/commit/11e62bd773485834a07364fb73f2c3decef818a6)), closes [#177](https://github.com/Hexalith/Hexalith.EventStore/issues/177)
* resolve Admin UI status indicator and theme toggle bugs (story 15-1) ([a265b56](https://github.com/Hexalith/Hexalith.EventStore/commit/a265b5651e14bf694ef77469977d550c633e51e2))
* resolve build errors and command dispatch bug in EventStoreAggregate ([35dcf22](https://github.com/Hexalith/Hexalith.EventStore/commit/35dcf22b232729f1ca125bf1888faf6931a88e29))
* **server:** register IHttpClientFactory for DaprDomainServiceInvoker ([#191](https://github.com/Hexalith/Hexalith.EventStore/issues/191)) ([bffe75a](https://github.com/Hexalith/Hexalith.EventStore/commit/bffe75ac7f9b4b8b55ea951059dfb8f8432d8474))
* **server:** rewrite DaprTenantQueryService to use query pipeline instead of direct state store ([6966c8c](https://github.com/Hexalith/Hexalith.EventStore/commit/6966c8c91089130df5872b1d29bd541df3bf79c4))
* **server:** support wildcard tenant routing in DomainServiceResolver ([a585521](https://github.com/Hexalith/Hexalith.EventStore/commit/a585521802265f3d26137c405b147f06ce9db776))
* **tenants:** fix tenant admin UI bugs — index persistence, command processing, and user display ([a8b9579](https://github.com/Hexalith/Hexalith.EventStore/commit/a8b9579384b5a45e9f7a8d358eb6b57572ecf6b1))
* **tenants:** update submodule and add sprint change proposal for tenant creation deadlock ([7013ea4](https://github.com/Hexalith/Hexalith.EventStore/commit/7013ea494b1826c116c691db099f89f36ed60a6d))
* **test:** align unknown-event tests with skip-unknown-events behavior ([c0ec5f5](https://github.com/Hexalith/Hexalith.EventStore/commit/c0ec5f5a94e20e62bd99f3c02062b2f1a81e4701))
* **ui:** fix breadcrumb rendering on home page and bUnit JS interop hangs ([769f6d3](https://github.com/Hexalith/Hexalith.EventStore/commit/769f6d32a395a5603498c06d01934654417b658f))
* wrap FluentDesignTheme around layout for proper dark mode rendering ([0e1b413](https://github.com/Hexalith/Hexalith.EventStore/commit/0e1b4130b87695bd471672e124ae2b6567946068))


### Features

* add backup CLI sub-subcommands completing story 17-6 ([e1a42c2](https://github.com/Hexalith/Hexalith.EventStore/commit/e1a42c2ed92d00a688ff0a0f2f344533670fe468)), closes [#158](https://github.com/Hexalith/Hexalith.EventStore/issues/158)
* **apphost:** add Hexalith.Tenants server to Aspire topology ([#188](https://github.com/Hexalith/Hexalith.EventStore/issues/188)) ([a97fe5b](https://github.com/Hexalith/Hexalith.EventStore/commit/a97fe5b154179b1dbc3e1517edb5971959e40966))

## [1.1.1](https://github.com/Hexalith/Hexalith.EventStore/compare/v1.1.0...v1.1.1) (2026-04-02)


### Bug Fixes

* **DaprRateLimitConfigSync:** improve logging for DAPR config store unavailability ([64babc2](https://github.com/Hexalith/Hexalith.EventStore/commit/64babc2d6016695e43c2455ec4cbd5f0fd4c6379))

# [1.1.0](https://github.com/Hexalith/Hexalith.EventStore/compare/v1.0.0...v1.1.0) (2026-04-01)


### Features

* **config:** update settings and add coderabbit configuration for review automation ([b072c08](https://github.com/Hexalith/Hexalith.EventStore/commit/b072c082579d6527293161fa644b1403faa8fee7))

# 1.0.0 (2026-04-01)


### Bug Fixes

* **admin:** resolve 4 data pipeline bugs causing empty Admin UI pages ([e81108d](https://github.com/Hexalith/Hexalith.EventStore/commit/e81108d23a32f1b5a5a810f893f8029ba12babf5))
* Apply 23 code review patches for story 15-2 activity feed ([bee95bd](https://github.com/Hexalith/Hexalith.EventStore/commit/bee95bd2a0eb8869b8ae1cca4272c5d0175c5773))
* Apply 23 code review patches for story 15-3 stream browser ([cf7cf50](https://github.com/Hexalith/Hexalith.EventStore/commit/cf7cf507a46f44aa2fa3c5576a8f06b8d4252b98))
* Apply 9 code review patches for story 15-5 projection dashboard ([#141](https://github.com/Hexalith/Hexalith.EventStore/issues/141)) ([4d93646](https://github.com/Hexalith/Hexalith.EventStore/commit/4d936467351600fbf7a073f011124cd4a6f7236e))
* bump .NET SDK pin from 10.0.102 to 10.0.103 ([0790586](https://github.com/Hexalith/Hexalith.EventStore/commit/07905864040e962bd3bf13e8ac09507e64fecae7))
* **ci:** Run dotnet test per-project for .NET SDK 10 compatibility ([5053164](https://github.com/Hexalith/Hexalith.EventStore/commit/505316472a673d72db7f699b56ee00a1dad6616e))
* **ci:** use full dapr init for Tier 2 integration tests ([#179](https://github.com/Hexalith/Hexalith.EventStore/issues/179)) ([f446a3f](https://github.com/Hexalith/Hexalith.EventStore/commit/f446a3f9b2a0f9e2ac226e435d471bfb364a24e7))
* **ci:** Use full dapr init for Tier 2 Server integration tests ([4a25603](https://github.com/Hexalith/Hexalith.EventStore/commit/4a25603ccc22b450981d4609514e2363c81b14b1))
* **ci:** Use platform-specific Dapr placement/scheduler ports ([091bcae](https://github.com/Hexalith/Hexalith.EventStore/commit/091bcae8f607ef7dc05a49bb77c3cca1d048808b))
* Correct non-Base32 test input length and remove redundant RegexOptions in MessageType and KebabConverter ([1e9efbd](https://github.com/Hexalith/Hexalith.EventStore/commit/1e9efbd3e439f101ded5ac12de5ffe7044fb94df))
* Harden Story 3.1 CorrelationId handling and identifier validation ([f906773](https://github.com/Hexalith/Hexalith.EventStore/commit/f90677301f44d734884fb3e5faad56d41d9ccb3f))
* refine event debugger UI, add missing tests, and update sprint artifacts (story 20-3) ([0ad3542](https://github.com/Hexalith/Hexalith.EventStore/commit/0ad35427a8ac56c0c6a73289f8f09be8a2eead9f))
* Remove duplicate .sln file that breaks CI restore ([ecbf561](https://github.com/Hexalith/Hexalith.EventStore/commit/ecbf5616d7dc8901efae6c698ae8db80bee2523a))
* remove missing Admin.UI.E2E project from solution file ([f54617b](https://github.com/Hexalith/Hexalith.EventStore/commit/f54617b454ca7d52419a57758b64a8c3199e4247))
* replace .sln references with .slnx across docs ([f825bf9](https://github.com/Hexalith/Hexalith.EventStore/commit/f825bf966fb88e82029ac1eac3dd9fbef65cc1f8))
* Resolve Story 7.5 Tier 3 test failures and address all code review findings ([55a2f70](https://github.com/Hexalith/Hexalith.EventStore/commit/55a2f7019aabc76089656f24698190049427af7e))
* Review follow-ups for stories 4.5 & 5.1, OpenTelemetry prep ([#40](https://github.com/Hexalith/Hexalith.EventStore/issues/40)) ([8fcb122](https://github.com/Hexalith/Hexalith.EventStore/commit/8fcb1221508998aa19c828f6f5c01c9e2d2aabde))
* **sample:** resolve Increment button errors and enable real-time SignalR refresh ([c6489ed](https://github.com/Hexalith/Hexalith.EventStore/commit/c6489ed5d87047b34e99747d36ded5453c4fb3c9))
* **story-16-4:** close review findings and mark done ([#72](https://github.com/Hexalith/Hexalith.EventStore/issues/72)) ([016c01b](https://github.com/Hexalith/Hexalith.EventStore/commit/016c01b26a3fc2651ef5d4085e0f6159dc78fe04))
* **tests:** add explicit log filter to unblock release pipeline ([#183](https://github.com/Hexalith/Hexalith.EventStore/issues/183)) ([1c5f368](https://github.com/Hexalith/Hexalith.EventStore/commit/1c5f368aa21bcb245fbefab38270224a2c2006c9))
* **tests:** update MCP DLL resolution logic to ensure correct path for dependencies ([12fbba0](https://github.com/Hexalith/Hexalith.EventStore/commit/12fbba03d086fbe23390c9f8c840ef061ec3dabd))
* tighten 5-3 review follow-up tests ([6f6cfaa](https://github.com/Hexalith/Hexalith.EventStore/commit/6f6cfaad1ef3de7f92c4c1ccc9696880ad58bda3))
* update sprint-status.yaml to reflect completed epics and their statuses ([14b8e82](https://github.com/Hexalith/Hexalith.EventStore/commit/14b8e82f4c246c1f3be7518118e7a7d10cfa121f))
* use VersionOverride for FluentUI packages in design directions prototype ([#94](https://github.com/Hexalith/Hexalith.EventStore/issues/94)) ([b9e7897](https://github.com/Hexalith/Hexalith.EventStore/commit/b9e78975cc2fb08c9272c3f10c12194f4b0ca312))


### Features

* Add Admin UI for managing tenants and authentication ([f311aa4](https://github.com/Hexalith/Hexalith.EventStore/commit/f311aa4e2962f049152dd1eb2ef980fbae171296))
* Add aggregate state inspector and diff viewer for story 15-4 ([bd45ab4](https://github.com/Hexalith/Hexalith.EventStore/commit/bd45ab4f6ba6f75eaecda3d266bdf5e5166f8dc2))
* Add AspireTopologyFixture to manage Aspire and Keycloak for security integration tests. ([ecb53d4](https://github.com/Hexalith/Hexalith.EventStore/commit/ecb53d4d6a007f525c878a7294563bce893f28ac))
* Add AssemblyScanner auto-discovery and harden aggregate/projection error handling ([18f2c4e](https://github.com/Hexalith/Hexalith.EventStore/commit/18f2c4e60674b2ffb1b120bab9e4aadada6e7f75))
* Add backup and restore console with export/import for story 16-4 ([7599a26](https://github.com/Hexalith/Hexalith.EventStore/commit/7599a2648d69078e17ad31345eacd8f972ca84da))
* add bisect tool with binary search for state divergence detection (story 20-2) ([431eb15](https://github.com/Hexalith/Hexalith.EventStore/commit/431eb1512024c242593e1f95e5414e5ae077ca51))
* add blame view with per-field provenance for aggregate state debugging (story 20-1) ([8d5f11a](https://github.com/Hexalith/Hexalith.EventStore/commit/8d5f11ad1c30f90ff2ea5c3aa356082273178e11))
* Add CD pipeline for automatic staging deployment on push to main ([dde2da7](https://github.com/Hexalith/Hexalith.EventStore/commit/dde2da7d126559ac5b9bc2b1ea6d72925cd5d8ef))
* Add CLI scaffold with System.CommandLine, global options, and health command for story 17-1 ([43f4a87](https://github.com/Hexalith/Hexalith.EventStore/commit/43f4a87376a9c827e4b1e9d287abe5937f53e3f2))
* add command sandbox test harness for stream detail page (story 20-4) ([db0d333](https://github.com/Hexalith/Hexalith.EventStore/commit/db0d333cafc5ab62335db054d129cec89ddfde3a))
* Add compaction manager page with job history and trigger dialog for story 16-3 ([17c8407](https://github.com/Hexalith/Hexalith.EventStore/commit/17c8407155885ba887ce9d5aea3233f6dfd7529f))
* Add connection profiles and shell completion for story 17-7 ([d0d351d](https://github.com/Hexalith/Hexalith.EventStore/commit/d0d351d558fba44b5a93eb24b657e40d5b3a647e))
* Add consistency checker with anomaly detection and data integrity verification for story 16-7 ([397f741](https://github.com/Hexalith/Hexalith.EventStore/commit/397f74160793194af4a77442565de326ee73520d))
* Add context-aware breadcrumbs with deep linking support for story 15-8 ([b400b47](https://github.com/Hexalith/Hexalith.EventStore/commit/b400b47ec4a8cbc269944e130be84fbc77d90f9d))
* Add convention-based projection discovery and configuration gating ([69f96d2](https://github.com/Hexalith/Hexalith.EventStore/commit/69f96d27933bad7f511e991405e6a1c8cc31c67a))
* Add core identity scheme and event metadata envelope ([923ad3b](https://github.com/Hexalith/Hexalith.EventStore/commit/923ad3b1c631dd6e0759d4079e00a5ee2fe9b11c))
* add correlation ID trace map for cross-aggregate event visualization (story 20-5) ([15c2382](https://github.com/Hexalith/Hexalith.EventStore/commit/15c23828c93f0671664815e1139452902ed511d8))
* Add counter sample /project endpoint for projection pipeline ([04d59d2](https://github.com/Hexalith/Hexalith.EventStore/commit/04d59d2e0c4135fd7f2682c37bce9aed754ceec2))
* add DAPR actor inspector with type registry, runtime config, and state viewer (story 19-2) ([9953bbb](https://github.com/Hexalith/Hexalith.EventStore/commit/9953bbbf35b544c4e8ac79164b15f7ea9b2852ef))
* add DAPR component health history timeline with background collection (story 19-5) ([91c483d](https://github.com/Hexalith/Hexalith.EventStore/commit/91c483d71ff870e6f03197de82b457e680a6c5c4))
* add DAPR component status dashboard (story 19-1) ([e57ec0a](https://github.com/Hexalith/Hexalith.EventStore/commit/e57ec0a0273ccf7339b61a7e1483194648a362db))
* Add DAPR component YAML files for Redis and PostgreSQL backend swap demo ([c1d5e2d](https://github.com/Hexalith/Hexalith.EventStore/commit/c1d5e2df4bf650b91cff0cda01700b873898ffee))
* add DAPR pub/sub delivery metrics dashboard (story 19-3) ([#169](https://github.com/Hexalith/Hexalith.EventStore/issues/169)) ([ab5cdec](https://github.com/Hexalith/Hexalith.EventStore/commit/ab5cdecf9181271633ab82e95fa8432f533953f2))
* add DAPR resiliency policy viewer with retry, circuit breaker, and timeout inspection (story 19-4) ([0f59bc4](https://github.com/Hexalith/Hexalith.EventStore/commit/0f59bc41d1efa82077f660833d4af78189712797))
* Add DaprSidecarUnavailableHandler for handling DAPR sidecar unavailability ([7a67ba6](https://github.com/Hexalith/Hexalith.EventStore/commit/7a67ba6e24639f1386f987caa976984a89d18c98))
* Add dead letter queue manager with retry, skip, and archive for story 16-6 ([d053fe8](https://github.com/Hexalith/Hexalith.EventStore/commit/d053fe86ca0e32319836f06a3f77b07a40d9557e))
* Add diagnostic tools and consistency checks for story 18-3 ([751ae68](https://github.com/Hexalith/Hexalith.EventStore/commit/751ae68e398ea69b116985a81a8f90800b1c8e04))
* Add dotnet tool packaging, --version flag, and CI smoke tests for story 17-8 ([3348171](https://github.com/Hexalith/Hexalith.EventStore/commit/3348171d6dba121693b89544423e37b6a8eac7d1))
* Add ETag actor projection notification tests and audit for Story 9-3 ([7bd36cc](https://github.com/Hexalith/Hexalith.EventStore/commit/7bd36cc91815644132ac21dd4a756ba70fb25d91))
* Add event type catalog with searchable registry for story 15-6 ([b68f158](https://github.com/Hexalith/Hexalith.EventStore/commit/b68f15864207ee21b782de3cb6e4325e1dcde3af))
* Add EventStoreAggregate base class, EventStoreDomain attribute, and NamingConventionEngine ([4e47cb6](https://github.com/Hexalith/Hexalith.EventStore/commit/4e47cb62ef2d2e6d5e67b366da64cebb0b43c24d))
* Add GitHub issue templates, PR template, and fix MCP config ([59ad02c](https://github.com/Hexalith/Hexalith.EventStore/commit/59ad02c0d37bfafe96c66892287bed083932a7ac))
* Add health dashboard with observability deep links for story 15-7 ([b6acc8b](https://github.com/Hexalith/Hexalith.EventStore/commit/b6acc8bdee076d1bbbb02310c56e7b468be92363))
* Add health subcommand CI/CD options and dapr sub-subcommand for story 17-4 ([02f3a20](https://github.com/Hexalith/Hexalith.EventStore/commit/02f3a20ddf06dd55786fd62c4f2ee527289fe235))
* Add IEventPayloadProtectionService for GDPR payload encryption ([#91](https://github.com/Hexalith/Hexalith.EventStore/issues/91)) ([a73bcd3](https://github.com/Hexalith/Hexalith.EventStore/commit/a73bcd3b142cf7e76d0df821e6faa821e5fe982f))
* Add immediate projection trigger with fire-and-forget background task ([c96503c](https://github.com/Hexalith/Hexalith.EventStore/commit/c96503c122b1884dd00d3b2a2a1eda7157fe5716))
* Add interactive command buttons on all pattern pages (Story 12-2) ([6b888d9](https://github.com/Hexalith/Hexalith.EventStore/commit/6b888d9c65f16f1b24e173602a09c22b7dc7459d))
* Add IQueryResponse compile-time enforcement audit and gap-fill tests for Story 9-5 ([3c4cf4f](https://github.com/Hexalith/Hexalith.EventStore/commit/3c4cf4f7c80cbc29687c681df172219a5c052e29))
* Add JWT and Keycloak authorization to the Command API, alongside new integration tests and Aspire infrastructure. ([6904602](https://github.com/Hexalith/Hexalith.EventStore/commit/69046020a7e68cc7307ab239cf1849a2ad4b9064))
* Add local documentation validation script ([42113e5](https://github.com/Hexalith/Hexalith.EventStore/commit/42113e507181196f9204c177bf2a1aef33d3c275))
* Add MCP server scaffold with stdio transport for story 18-1 ([c13ecae](https://github.com/Hexalith/Hexalith.EventStore/commit/c13ecae0244dfb3bad94b619cb8434a2834914d9))
* Add projection event DTOs and tests for event handling ([fd136a0](https://github.com/Hexalith/Hexalith.EventStore/commit/fd136a0934913b9d887ffcdd7688b37c8cdb7427))
* Add projection subcommand with list, status, pause, resume, and reset for story 17-3 ([8543c83](https://github.com/Hexalith/Hexalith.EventStore/commit/8543c83bfd2f56857f93bff176ea33386ecfd2b5))
* Add query actor in-memory page cache audit and gap-fill tests for Story 9-4 ([92452aa](https://github.com/Hexalith/Hexalith.EventStore/commit/92452aaa84ca3a7d17982dd18ef3d250249220d8))
* Add query contracts and routing model for Story 9-1 ([dcabace](https://github.com/Hexalith/Hexalith.EventStore/commit/dcabace7919c408afdef76e2f3b4c8232607f69b))
* Add QueryExecutionFailedException and handler for query execution failures ([1407a14](https://github.com/Hexalith/Hexalith.EventStore/commit/1407a145bdea0ce7dbebc17ae307c92e669ff028))
* Add quickstart smoke test project validating documented Counter behavior (Story 13-1) ([#79](https://github.com/Hexalith/Hexalith.EventStore/issues/79)) ([fba3ddb](https://github.com/Hexalith/Hexalith.EventStore/commit/fba3ddb48f87d818b0b703ba56bdefa0553b72a8))
* Add read-only MCP tools for streams, projections, types, and metrics for story 18-2 ([7ad6389](https://github.com/Hexalith/Hexalith.EventStore/commit/7ad6389cd86885f1d79cb27866ef8ce24ce3f08f))
* Add self-routing ETag audit tests and fill coverage gaps for Story 9-2 ([7bdf7d1](https://github.com/Hexalith/Hexalith.EventStore/commit/7bdf7d1bac9cf3ad17c6eaff1d0fa63c1b436a15))
* Add snapshot management page with auto-snapshot policies for story 16-2 ([ce95956](https://github.com/Hexalith/Hexalith.EventStore/commit/ce959564df215114adc556c02da163d25800cb4c))
* Add snapshot subcommand with create, policies, set-policy, and delete-policy for story 17-6 ([c928e9f](https://github.com/Hexalith/Hexalith.EventStore/commit/c928e9fb73da01894482122aa3e4fb24ff1d1547))
* Add sprint change proposals for renaming AppIds and implementing convention-based message routing ([ad377e7](https://github.com/Hexalith/Hexalith.EventStore/commit/ad377e704aefbb601e3c0188b88ef5ef768edf94))
* add step-through event debugger with VCR controls and auto-play (story 20-3) ([36c5c35](https://github.com/Hexalith/Hexalith.EventStore/commit/36c5c35e95996aac7af3e5b3af9bdf0d6e23288d))
* Add storage growth analyzer with treemap visualization for story 16-1 ([4a10138](https://github.com/Hexalith/Hexalith.EventStore/commit/4a10138c84ff2862c9b5dde3f49930666054fca6))
* Add Story 4.2 documentation for resilient publication and backlog draining ([6dd9bad](https://github.com/Hexalith/Hexalith.EventStore/commit/6dd9badecc6ec6f48926b262d0f8dd3656a21e37))
* Add stream browser with command/event timeline view (story 15-3) ([a7a8473](https://github.com/Hexalith/Hexalith.EventStore/commit/a7a8473e2d7bb0a677980a781dee7f9aa869d845))
* Add stream subcommand with query, list, events, and state for story 17-2 ([40201a2](https://github.com/Hexalith/Hexalith.EventStore/commit/40201a207f6a270d7a423c9f9147977f6e43a02f))
* add tenant context and investigation session state tools (story 18-5) ([6f6a998](https://github.com/Hexalith/Hexalith.EventStore/commit/6f6a998540e3b8db16e9b900affdee50a5d4280b))
* Add tenant management with quotas and user onboarding for story 16-5 ([c687d44](https://github.com/Hexalith/Hexalith.EventStore/commit/c687d443b88f6fb1edc7935fc815355000eab140))
* Add tenant subcommand with list, detail, quotas, users, compare, and verify for story 17-5 ([f864c8a](https://github.com/Hexalith/Hexalith.EventStore/commit/f864c8abff9d46f0f955f3312f2180fbdc076175))
* Add Tier 3 E2E contract tests with Aspire topology (Story 7.5) ([#47](https://github.com/Hexalith/Hexalith.EventStore/issues/47)) ([f67839b](https://github.com/Hexalith/Hexalith.EventStore/commit/f67839b809f3d8863efb5823ba6bcf420fc2b878))
* Add UseEventStore activation, cascading config, and story 16-7 spec ([7dde37b](https://github.com/Hexalith/Hexalith.EventStore/commit/7dde37b99c744facd76328d8902681a5c0e6c83d))
* Add write tools with approval gates for backup, projection, and consistency commands (story 18-4) ([94fc75e](https://github.com/Hexalith/Hexalith.EventStore/commit/94fc75e555969ea15098b6e903f21bc33086d93e))
* Apply code review fixes for Stories 6.4 and 7.1 ([e69ee0f](https://github.com/Hexalith/Hexalith.EventStore/commit/e69ee0ffa6f2c4cebeb018e1cbaeb0535b07f3a0))
* Apply code review fixes for story 15-7 health dashboard ([798c44d](https://github.com/Hexalith/Hexalith.EventStore/commit/798c44d412c1a824d9d3c6724337eb30c2f3d61e))
* Apply code review fixes for Story 7.2 Dapr component configurations ([df03692](https://github.com/Hexalith/Hexalith.EventStore/commit/df0369245e027207a711214aea2ab2d25961a54c))
* Apply code review fixes for Story 7.4 integration tests ([18ed5e4](https://github.com/Hexalith/Hexalith.EventStore/commit/18ed5e4b729e4e1c6b70444f2bdcd2ca872444d3))
* Apply semantic status colors to CounterCommandForm (Story 12-1) ([af3f4db](https://github.com/Hexalith/Hexalith.EventStore/commit/af3f4dbf295f4417faa46350e76e188f8b7f2f47))
* Complete Epic 16 fluent API stories (16-5 through 16-10) ([a7a3327](https://github.com/Hexalith/Hexalith.EventStore/commit/a7a3327e17656a48bd05a8b4ecfabf93c97520f8))
* Complete implementation of IQueryResponse enforcement and runtime projection type discovery ([6a241e6](https://github.com/Hexalith/Hexalith.EventStore/commit/6a241e672914f041b3cf254d924b920be58548f4))
* Complete Story 10-1 CONTRIBUTING.md and CODE_OF_CONDUCT.md ([37f758c](https://github.com/Hexalith/Hexalith.EventStore/commit/37f758c2a987d8f132a42c5c05dfbf9891ab3ffd))
* Complete Story 4.2 — Resilient Publication & Backlog Draining verification ([e2bc377](https://github.com/Hexalith/Hexalith.EventStore/commit/e2bc377bc704a9f7ce1ae2b79607e4c7919be0ad))
* Complete Story 5.5 E2E Security Testing with Keycloak ([d865a0c](https://github.com/Hexalith/Hexalith.EventStore/commit/d865a0cc9bf2e3aab453bdf600be3bffcdb6672a))
* Complete Story 6.1 OpenTelemetry Tracing verification ([3543db5](https://github.com/Hexalith/Hexalith.EventStore/commit/3543db5d139b4e9954264a60ed2984adee75cfa6))
* Complete Story 6.2 Structured Logging verification ([5b81788](https://github.com/Hexalith/Hexalith.EventStore/commit/5b817888c4e8d28ce13967a8e9f309da4b29f751))
* Complete Story 6.3 Health and Readiness Endpoints ([ad9aa77](https://github.com/Hexalith/Hexalith.EventStore/commit/ad9aa777f17d98234fd680be66bfbbc02fbbe102))
* Complete Story 7.1 Configurable Aggregate Snapshots ([fecba85](https://github.com/Hexalith/Hexalith.EventStore/commit/fecba8599588fe4e3956d47672e84bd80eb977c8))
* Complete Story 8-2 README rewrite with progressive disclosure ([#61](https://github.com/Hexalith/Hexalith.EventStore/issues/61)) ([9270960](https://github.com/Hexalith/Hexalith.EventStore/commit/92709602bbdc85db04d9ef689c3c1f512fa69cb9))
* Complete Story 8-3 Prerequisites & Local Dev Environment page ([225485f](https://github.com/Hexalith/Hexalith.EventStore/commit/225485f0472762fe375ba7b8fe0387211f48bac8))
* Complete Story 8-4 Choose the Right Tool decision aid page ([f40d177](https://github.com/Hexalith/Hexalith.EventStore/commit/f40d177d2028f1e546ce28abb7eec2da2fb7efaf))
* Complete Story 8-5 animated GIF demo capture and update README ([22b09f8](https://github.com/Hexalith/Hexalith.EventStore/commit/22b09f8152b4ae84d8d7d460b6e518094e919871))
* Complete Story 8-5 GIF capture and address review findings ([656286c](https://github.com/Hexalith/Hexalith.EventStore/commit/656286cf6bf2d8a9ed10cacff52c6cb152b6288a))
* Complete Story 8-6 CHANGELOG initialization with full project history ([acf9e24](https://github.com/Hexalith/Hexalith.EventStore/commit/acf9e24b3c7495fe18c26bf307a39f22cea47910))
* Complete Story 8.1 Aspire AppHost & DAPR topology with prerequisite validation ([96e725f](https://github.com/Hexalith/Hexalith.EventStore/commit/96e725f2caf0fe03adfcf3f1d58580f5ca2b93e7))
* Complete Story 8.3 by finalizing NuGet client package, adding XML documentation, and updating sprint status ([53903b7](https://github.com/Hexalith/Hexalith.EventStore/commit/53903b77d4fb79035ef070cd75dd9d7abbd4a98b))
* Complete Story 8.6 deployment manifests and environment portability validation ([f5f76bd](https://github.com/Hexalith/Hexalith.EventStore/commit/f5f76bd851ba6ba56c0b2e3530e595fd72ea9a39))
* Complete Story 8.7 CI/CD pipeline validation and gap fixes ([f19083e](https://github.com/Hexalith/Hexalith.EventStore/commit/f19083e372c0b874ae7761a0801ea9bc3ca12efe))
* Complete Story 9-1 quickstart guide with end-to-end walkthrough ([db75d0f](https://github.com/Hexalith/Hexalith.EventStore/commit/db75d0f24bbd33d3dc1ff14bd39a3a5585821a6a))
* Create Story 7.7 - Aspire publisher deployment manifests ([761c1a6](https://github.com/Hexalith/Hexalith.EventStore/commit/761c1a68d024bac78780c0d91066a4d33dab2751))
* **docs:** Add Azure Container Apps deployment guide and Bicep templates (Story 14-3) ([02bd290](https://github.com/Hexalith/Hexalith.EventStore/commit/02bd29067ef4bfb63e3c381449a24dac3024d664))
* **docs:** Add configuration reference (Story 15-1) ([fca1cd2](https://github.com/Hexalith/Hexalith.EventStore/commit/fca1cd2c5b75d6a0c2f4d6b5630f78dfe2935fae))
* **docs:** Add DAPR component configuration reference (Story 14-5) ([0525b7a](https://github.com/Hexalith/Hexalith.EventStore/commit/0525b7abda3d39431dc85198a5762094629a6053))
* **docs:** add DAPR FAQ deep dive (Story 15-6) ([#97](https://github.com/Hexalith/Hexalith.EventStore/issues/97)) ([acd38cf](https://github.com/Hexalith/Hexalith.EventStore/commit/acd38cfc82f290110cf6ba5c12e8cf5fc31fd966))
* **docs:** Add deployment progression guide connecting all deployment environments (Story 14-4) ([f04bdf7](https://github.com/Hexalith/Hexalith.EventStore/commit/f04bdf7067af0f2131b045d6c706ed29be5a253e))
* **docs:** Add disaster recovery procedure (Story 14-8) ([bb1ee66](https://github.com/Hexalith/Hexalith.EventStore/commit/bb1ee665f979163a5536e8af132174e59aa3b9eb))
* **docs:** Add Kubernetes deployment guide and FR traceability matrix ([27b9933](https://github.com/Hexalith/Hexalith.EventStore/commit/27b993375e7116efbe36c33a99e8290806140a96))
* **docs:** Add Kubernetes deployment guide and sample manifests (Story 14-2) ([c2f5a56](https://github.com/Hexalith/Hexalith.EventStore/commit/c2f5a567251b66df8a02881f6b5affdbd9da0d0f))
* **docs:** Add security model documentation (Story 14-6) ([c3574c7](https://github.com/Hexalith/Hexalith.EventStore/commit/c3574c7c1c0dd1bc17e50b33456783537452661d))
* **docs:** Add troubleshooting guide (Story 14-7) ([d07f00e](https://github.com/Hexalith/Hexalith.EventStore/commit/d07f00e50df9ac2b5891677a3cd7d13c29ba8a72))
* **docs:** Update Docker Compose deployment guide and configuration status to done ([2700786](https://github.com/Hexalith/Hexalith.EventStore/commit/2700786aafd26b1ae1ece810dc87e4a26bce2eb6))
* **docs:** Update local validation script documentation and improve formatting ([cd6ec4b](https://github.com/Hexalith/Hexalith.EventStore/commit/cd6ec4b05dcb76a60898662a65a0ee9d43ab1970))
* Enable OpenAPI and Swagger UI for Hexalith EventStore Admin API ([f7889bc](https://github.com/Hexalith/Hexalith.EventStore/commit/f7889bc37f8fb9558fd75704dbaa9a3ea909675d))
* Enhance command handling with domain rejection support and improved error responses ([b5d0618](https://github.com/Hexalith/Hexalith.EventStore/commit/b5d06180ec6fed92f8bc9d672299841ed1910a6c))
* Enhance command processing with result payload support ([f06fba3](https://github.com/Hexalith/Hexalith.EventStore/commit/f06fba3fd0a97bef799427111ae8d401f2ab72b3))
* enhance domain service resolution with DAPR config store fallback ([679a971](https://github.com/Hexalith/Hexalith.EventStore/commit/679a971727ce0a7321f5d11a34439ccbfa2d3298))
* Enhance error handling in CounterQueryService and update SignalR configuration in Program.cs ([f2a1313](https://github.com/Hexalith/Hexalith.EventStore/commit/f2a1313c4068b28fd6e9a32cdad39b095961214e))
* Enhance SecretsProtectionTests to skip specific patterns and improve hardcoded secret detection logic ([43b285b](https://github.com/Hexalith/Hexalith.EventStore/commit/43b285b2f6f2e932446a7b3060690526a26088db))
* Enhance UseEventStore validation and add external blockers template ([b422bab](https://github.com/Hexalith/Hexalith.EventStore/commit/b422babfee54522e2ecfd14d61235991690e5a27))
* Enhance verification tasks and clarify baseline requirements for claims-based command authorization ([91fd854](https://github.com/Hexalith/Hexalith.EventStore/commit/91fd854b69247692af2aa62f2dd26b7a990dbb9a))
* Epic 1 Stories 1.1, 1.2, 1.3 — Domain Contract Foundation ([#98](https://github.com/Hexalith/Hexalith.EventStore/issues/98)) ([493bcd8](https://github.com/Hexalith/Hexalith.EventStore/commit/493bcd83dc9e483801d09d5cc8591f9d7b5d4628))
* **events:** add cross-stream event browser ([#182](https://github.com/Hexalith/Hexalith.EventStore/issues/182)) ([c5ef3fb](https://github.com/Hexalith/Hexalith.EventStore/commit/c5ef3fb9cd04b6dfdf7bedbba335c53d202d18eb))
* **events:** implement cross-stream event browser on Events page with filtering and pagination ([005089c](https://github.com/Hexalith/Hexalith.EventStore/commit/005089ce41c0951f8dbfdced6b3b9cc74e878b1e))
* Extend SubmitCommand to include IsGlobalAdmin flag and update related tests ([2d4ce03](https://github.com/Hexalith/Hexalith.EventStore/commit/2d4ce03fae8584b1f0bf19fd7174c7bb778a17d0))
* finalize Story 10-4 discussions setup and review fixes ([#68](https://github.com/Hexalith/Hexalith.EventStore/issues/68)) ([84dd1a4](https://github.com/Hexalith/Hexalith.EventStore/commit/84dd1a437664f2914cac9bd2a383da2778ce65c9))
* Implement AdminStreamApiClient for interacting with Admin.Server API ([ca242c3](https://github.com/Hexalith/Hexalith.EventStore/commit/ca242c30be7e2a0cd646518a85c20f9d153f87ca))
* Implement Aspire integration for Hexalith EventStore with Dapr components and Redis, alongside a security-focused Aspire topology integration test fixture. ([8e14896](https://github.com/Hexalith/Hexalith.EventStore/commit/8e148962ff74ec074cbe9654d4278b55ea5d4e36))
* Implement at-least-once delivery and DAPR retry policies ([42bcd85](https://github.com/Hexalith/Hexalith.EventStore/commit/42bcd85409a61748dff41cee21a573a0d72d43e1))
* Implement backpressure handling in command API ([0748651](https://github.com/Hexalith/Hexalith.EventStore/commit/07486513b8752b49bdb5e385bd4ebe8ab3d41d68))
* Implement Command API authorization and validation behaviors, add Dapr domain service invoker, and introduce structured logging completeness tests. ([b9c126a](https://github.com/Hexalith/Hexalith.EventStore/commit/b9c126ae31308694dfffef64c05f8d98a881d253))
* Implement Command Status Query Endpoint with RFC 7807 Compliance ([11ff2f0](https://github.com/Hexalith/Hexalith.EventStore/commit/11ff2f09cb09c6a86a2ec117d45938416b0daf24))
* Implement Domain Processor State Rehydrator and DomainServiceCurrentState ([fd45dd0](https://github.com/Hexalith/Hexalith.EventStore/commit/fd45dd0468f9867c6f220c073d135528d6323f50))
* Implement EventReplayProjectionActor and projection state storage ([d390695](https://github.com/Hexalith/Hexalith.EventStore/commit/d390695214dffa63d9f958b80f2ad39584f6163a))
* Implement multi-domain support with Greeting aggregate and update routing logic ([0f9b28f](https://github.com/Hexalith/Hexalith.EventStore/commit/0f9b28f72bbc674841ad0dd94cbe9a2966338988))
* Implement per-consumer rate limiting alongside existing per-tenant limits ([3edd174](https://github.com/Hexalith/Hexalith.EventStore/commit/3edd1748c43c0789f1569916e363a45aa5e00894))
* Implement Projection Contract DTOs and AggregateActor Event Reading ([14f9647](https://github.com/Hexalith/Hexalith.EventStore/commit/14f96472a6c9dc641d554e65615388809bb57a74))
* Implement runtime projection type discovery for ETag caching ([1ed0a3b](https://github.com/Hexalith/Hexalith.EventStore/commit/1ed0a3b74276c65b5ef38ef66a967dce3915b24b))
* Implement self-routing ETag architecture for query pipeline ([1490138](https://github.com/Hexalith/Hexalith.EventStore/commit/14901384e7aec8379352001b27883f874ad1bc6f))
* Implement SignalR client for real-time projection change notifications ([4a84ad3](https://github.com/Hexalith/Hexalith.EventStore/commit/4a84ad3d8c4dabc8a4208de67cb2515ce0c9a6ae))
* Implement Stories 7.5/7.6 - CI/CD pipelines, wire-safe domain results, and test improvements ([d5d783e](https://github.com/Hexalith/Hexalith.EventStore/commit/d5d783ed3c327694246d10e8643d589182cedd89))
* Implement Story 1.4 — Pure Function Contract & EventStoreAggregate Base ([4b122e5](https://github.com/Hexalith/Hexalith.EventStore/commit/4b122e5ea690141647d285b125db4ba55bc4bd69))
* Implement Story 1.5 — Add CommandStatus enum, ITerminatable interface, and aggregate tombstoning support ([fc46ddd](https://github.com/Hexalith/Hexalith.EventStore/commit/fc46dddaec291a1c5e341d0a495ff2dc012a6296))
* Implement Story 3.2 — Command Validation & 400 Error Responses ([#100](https://github.com/Hexalith/Hexalith.EventStore/issues/100)) ([ede5278](https://github.com/Hexalith/Hexalith.EventStore/commit/ede5278c446b67d4b6a7b7562a08f44170224127))
* Implement Story 3.3 — Command Status Query Endpoint refinements ([ed21090](https://github.com/Hexalith/Hexalith.EventStore/commit/ed21090105af516ef2f6fa680191c118aecbd369))
* Implement Story 3.4 — Dead-Letter Routing & Command Replay hardening ([1eeb282](https://github.com/Hexalith/Hexalith.EventStore/commit/1eeb28274461dd72864a5785b56c02658fd8ed35)), closes [#10](https://github.com/Hexalith/Hexalith.EventStore/issues/10)
* Implement Story 3.5 — Concurrency, Auth & Infrastructure Error Responses ([23c6813](https://github.com/Hexalith/Hexalith.EventStore/commit/23c681362d77c67aa031706e37e85003c5a34652))
* Implement Story 3.6 — OpenAPI Specification & Swagger UI ([565a51c](https://github.com/Hexalith/Hexalith.EventStore/commit/565a51ceb6d991883f44c9eb0d402adf238ce4bf))
* Implement Story 4.3 — Per-Aggregate Backpressure ([85f55a4](https://github.com/Hexalith/Hexalith.EventStore/commit/85f55a4cdf6e8467890988df24d47abeccfcb4d6))
* Implement Story 7.7 - Aspire publisher deployment manifests ([4ceac13](https://github.com/Hexalith/Hexalith.EventStore/commit/4ceac13f4f903b520a000cfa6dd26ea32d17f6af))
* Implement Story 7.8 - Domain service hot reload validation ([6bf689b](https://github.com/Hexalith/Hexalith.EventStore/commit/6bf689b1d38d7380b761907e6967c941229a7328))
* Implement Story 8.4 by adding Greeting domain service registration, validating multi-domain hot reload, and updating sprint status ([bfe66e5](https://github.com/Hexalith/Hexalith.EventStore/commit/bfe66e58fdb566ce1784fb26d4d11d9520145a43))
* Integrate Hexalith.Commons.UniqueIds for ULID generation and update related documentation ([fef589f](https://github.com/Hexalith/Hexalith.EventStore/commit/fef589fd4db29b91c8c015db89062cb4fb5942a6))
* Introduce AspireTopologyFixture for E2E security integration tests, managing Aspire distributed application setup and Keycloak readiness. ([d2176ad](https://github.com/Hexalith/Hexalith.EventStore/commit/d2176adab89d9ddcdadb62fa92cc882a549ad595))
* Mark story 10-1 done, prepare story 10-2, and refactor SignalR notifier test ([6463ea7](https://github.com/Hexalith/Hexalith.EventStore/commit/6463ea73c1da93727c251dc93cd52fb2dec2f99e))
* Mark story 10-2 as done and update sprint status ([c61948a](https://github.com/Hexalith/Hexalith.EventStore/commit/c61948ad8e67bf33296503f18f77ae4c0f3dc459))
* **query:** add ProjectionType for flexible query routing ([b284296](https://github.com/Hexalith/Hexalith.EventStore/commit/b28429634283f83a6b0d142131dd99bd2dbd8147))
* Refactor SecretsProtectionTests to enhance pattern skipping and optimize hardcoded secret detection logic ([66999db](https://github.com/Hexalith/Hexalith.EventStore/commit/66999dbd2f21c516cd1d987a186591704b9cfb62))
* Stories 4.2 & 4.3 - Topic isolation and at-least-once delivery ([#38](https://github.com/Hexalith/Hexalith.EventStore/issues/38)) ([452962a](https://github.com/Hexalith/Hexalith.EventStore/commit/452962aec0c22ad63d5b95d134ea699d8d9d007f))
* Stories 4.4-4.5 & 5.1-5.4 - Resilience, dead-letter & security ([#39](https://github.com/Hexalith/Hexalith.EventStore/issues/39)) ([cbf367d](https://github.com/Hexalith/Hexalith.EventStore/commit/cbf367de3289202c4c80a61c440d65bd26b9ad9f))
* Stories 5.1 & 5.2 - DAPR access control & data path isolation verification ([47affbc](https://github.com/Hexalith/Hexalith.EventStore/commit/47affbc83bd8a7abbf7d9c9ee7db7236b451acc7))
* Stories 5.3, 6.2 & 6.3 - PubSub topic isolation, structured logging & dead-letter tracing ([1f9995a](https://github.com/Hexalith/Hexalith.EventStore/commit/1f9995ae810974645fe51289fc69bf707837f854))
* Stories 6.1-6.4 - Observability, telemetry & health check instrumentation ([427bb29](https://github.com/Hexalith/Hexalith.EventStore/commit/427bb29f5593e6f41863b58094857799d0e54a3a))
* Story 5.4 - Security audit logging & payload protection ([141e002](https://github.com/Hexalith/Hexalith.EventStore/commit/141e0023b15aea55f169a3f4524b40a3c561ec06))
* Story 6.4 - Implement Dapr health check endpoints with environment-aware responses ([b7f617c](https://github.com/Hexalith/Hexalith.EventStore/commit/b7f617ce5a86dfee386755f75e842d2ed41bb90d))
* Support 3-param Handle methods with CommandEnvelope ([d9d03b2](https://github.com/Hexalith/Hexalith.EventStore/commit/d9d03b2f277d6eb149860414b1a66a11be3a87ba))
* Update Aspire package versions to 13.1.2 in project files ([3a3f15f](https://github.com/Hexalith/Hexalith.EventStore/commit/3a3f15f8ade960a795fd5776301ce49005c0fa34))
* update Aspire package versions to 13.2.0 ([0b654b1](https://github.com/Hexalith/Hexalith.EventStore/commit/0b654b1961026fd922e20dfb4e11d92b8afcfabb))
* Update Blazor UI components and services for authentication and SignalR enhancements ([497b133](https://github.com/Hexalith/Hexalith.EventStore/commit/497b1336e6bc8393ed7c0db10573f4832ef668e5))
* Update CLAUDE.md and sprint-status.yaml for SignalR.Tests integration and three-tier test pyramid completion ([ce10f26](https://github.com/Hexalith/Hexalith.EventStore/commit/ce10f261f4e93d8bfce9f48462873795f1296814))
* Update DAPR component YAML files for Redis and PostgreSQL backend swap demo ([623adab](https://github.com/Hexalith/Hexalith.EventStore/commit/623adab0f3d4dd6a0a2ac6f2980bdc838e5ee8f0))
* Update Dapr health check implementation to use gRPC metadata API for improved reliability and adjust health check timeout settings ([612336b](https://github.com/Hexalith/Hexalith.EventStore/commit/612336b58f2d83b2b0f3c3f3687b81499884c83b))
* Update ETag fetch logic to prioritize non-empty ProjectionType over request.Domain ([2cf1410](https://github.com/Hexalith/Hexalith.EventStore/commit/2cf1410b49fffe63ebc37c90bfd4977bc5562c46))
* Update logging configuration to include LuckyPennySoftware.MediatR.License and enhance event deserialization in EventStoreAggregate ([e85f64d](https://github.com/Hexalith/Hexalith.EventStore/commit/e85f64d854be051a3d362078bd694a561e46e68e))
* Update README and documentation structure for Hexalith.EventStore ([65eef1a](https://github.com/Hexalith/Hexalith.EventStore/commit/65eef1ac9fd599124e81d575243cfe34026023bc))
* Update sprint status and add local validation scripts ([a714fc0](https://github.com/Hexalith/Hexalith.EventStore/commit/a714fc080444a51c2e4597e631bbf096cdf683e8))
* Update sprint status and add new tests for SignalR hub negotiation and projection change notifier ([d78da48](https://github.com/Hexalith/Hexalith.EventStore/commit/d78da489d49757f23d539961c8bf5302104e3db2))
* Update sprint status and add Story 4.1 documentation; enhance command/query validation with XML comments ([50b6e75](https://github.com/Hexalith/Hexalith.EventStore/commit/50b6e7538ab316fb1976fc16d1abd4d5f7456920))
* Update sprint status and add Story 7.2 for Per-Tenant Rate Limiting ([e2eeec8](https://github.com/Hexalith/Hexalith.EventStore/commit/e2eeec8ab5554134e8a6a82ef300097788f6efb3))
* Update sprint status and add Story 8.2 for Counter Sample Domain Service ([f22e5ee](https://github.com/Hexalith/Hexalith.EventStore/commit/f22e5ee10486ba7894c154e3c2ebf90ffc6f4ddb))
* Update sprint status and implement claims-based command authorization ([6ae83e1](https://github.com/Hexalith/Hexalith.EventStore/commit/6ae83e14dc6578166a73146f57c3f905996dc6d1))
* Update sprint status and implement command status enum with aggregate tombstoning ([671e452](https://github.com/Hexalith/Hexalith.EventStore/commit/671e45239ac3923e88f83b2d9d8878e1712c6bd9))
* Update sprint status and implement resilient publication & backlog draining ([1fd40a8](https://github.com/Hexalith/Hexalith.EventStore/commit/1fd40a833185e5c31c155a1f3e14ac299bb4661f))
* Update sprint status and implement various tests for command authorization and event persistence ([61e05d3](https://github.com/Hexalith/Hexalith.EventStore/commit/61e05d3f3dd555751c9051578e74c90278a09d95))
* Update sprint status for DAPR component variants demo to ready-for-dev ([68d5dd4](https://github.com/Hexalith/Hexalith.EventStore/commit/68d5dd402715f169ab59085f8f0d0857118d7714))
* Update status to done for story 17-8: .NET Tool Packaging and Distribution ([049dc7b](https://github.com/Hexalith/Hexalith.EventStore/commit/049dc7b9e2c4efa29b2584b6a018e675d29a1188))
* Update Story 5.4 for DAPR Service-to-Service Access Control ([726ccf8](https://github.com/Hexalith/Hexalith.EventStore/commit/726ccf862bc3866101c49961a240f3227d2d6e0d))
* Update story status for claims-based command authorization and DAPR service-to-service access control ([3b8d5bc](https://github.com/Hexalith/Hexalith.EventStore/commit/3b8d5bca86f5bd55df00926069fb33b62e044778))


### BREAKING CHANGES

* Validation error responses now use
type="https://hexalith.io/problems/validation-error",
title="Command Validation Failed", and a flat "errors" dictionary.
The legacy "validationErrors" array and "errorsDictionary" extensions
have been removed.

- Created shared ValidationProblemDetailsFactory as single source of truth
- Unified model-state 400 responses via ApiBehaviorOptions
- Removed exception detail leakage from GlobalExceptionHandler 500 responses
- Added 8 Tier 2 unit tests for ValidationExceptionHandler
- Updated integration test assertions for new format

Co-authored-by: Claude Opus 4.6 (1M context) <noreply@anthropic.com>

# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

#### Core SDK and Contracts

- `Hexalith.EventStore.Contracts` NuGet package with `EventEnvelope`, `AggregateIdentity`, `CommandEnvelope`, and `DomainResult` core types
- `Hexalith.EventStore.Client` NuGet package with `IDomainProcessor` pure function contract and `DomainProcessorBase<TState>` generic helper
- `Hexalith.EventStore.Testing` NuGet package with `InMemoryStateManager`, `FakeDomainServiceInvoker`, and fluent test builders
- Multi-project solution structure with centralized package management and .NET 10, DAPR 1.16.x, and Aspire 13.1.x
- Canonical multi-tenant identity derivation (`tenant:domain:aggregate-id`) via `AggregateIdentity`
- `CommandStatus` enum tracking the full command lifecycle from Received through Completed or Rejected

#### Command API Gateway

- RESTful command submission endpoint (`POST /api/v1/commands`) with 202 Accepted async processing
- RFC 7807 Problem Details error responses with field-level validation errors
- JWT Bearer authentication with `eventstore:tenant` claims transformation for multi-tenant isolation
- Per-endpoint authorization via MediatR `AuthorizationBehavior`
- Command status tracking endpoint (`GET /api/v1/commands/status/{correlationId}`) with 24-hour TTL
- Command replay endpoint (`POST /api/v1/commands/replay/{correlationId}`) for deterministic reprocessing
- ETag-based optimistic concurrency conflict detection with 409 Conflict responses
- Per-tenant sliding window rate limiting with 429 Too Many Requests enforcement
- OpenAPI 3.1 contract and interactive Swagger UI with Counter domain examples
- MediatR pipeline with logging, validation, and authorization behaviors

#### Command Processing and Event Storage

- Command routing to DAPR actors based on canonical multi-tenant identity
- Idempotent command deduplication via correlation ID caching
- Multi-tenant and multi-domain isolation at the actor level
- Domain service invocation via DAPR service-to-service calls
- Event stream reconstruction from persistent storage with snapshot optimization
- Atomic event persistence with gapless sequence numbering via `IActorStateManager`
- Composite storage key strategy preventing cross-tenant collisions
- Configurable snapshot creation for rehydration performance optimization
- Actor state machine with checkpointed stages and automatic recovery from infrastructure failures

#### Event Distribution

- CloudEvents 1.0 standard event publication via DAPR pub/sub
- Per-tenant per-domain topic isolation (`{tenant}.{domain}.events` pattern)
- At-least-once delivery with exponential backoff retry policies
- Persist-then-publish resilience with reminder-based drain for publication failures
- Dead-letter routing with full command context and failure details

#### Multi-Tenant Security

- DAPR access control policies with deny-by-default service-to-service communication
- Data path isolation verification across actor identity, DAPR policies, and command validation
- Pub/sub topic scoping restricting tenant and service access to event topics
- Security audit logging and payload protection for sensitive data

#### Observability

- End-to-end OpenTelemetry trace instrumentation across the complete command lifecycle
- Structured logging at every pipeline stage with correlation IDs
- Dead-letter-to-origin tracing for failed event investigation
- Health check endpoints (`/health`) with per-component status reporting
- Readiness check endpoints (`/alive`) for Kubernetes probes

#### Sample Application and CI/CD

- Counter domain service sample implementing the pure function programming model
- Local DAPR component configurations for Redis-backed development
- Production DAPR component templates for PostgreSQL, Kafka, and other backends
- Integration test suite with DAPR test containers
- End-to-end contract tests with Aspire topology
- GitHub Actions CI/CD pipeline with automated build, test, and NuGet publishing
- Aspire publisher deployment manifests for container orchestration
- Domain service hot-reload validation for local development

#### Documentation

- Progressive disclosure README with comparison table and architecture diagram
- Local development prerequisites page with .NET SDK, Docker, and DAPR CLI setup
- Decision aid helping developers evaluate Hexalith against alternatives
- Animated GIF demo of the quickstart workflow
- Documentation folder structure and page conventions
- CHANGELOG initialization with complete project history
