using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

public sealed partial class SecretsProtectionTests
{
    /// <summary>
    /// Historical evidence lines whose bytes are sealed. A changed file loses the exemption.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string Sha256, string[] LineHashes)> SealedHistoricalSecretFiles =
        new Dictionary<string, (string Sha256, string[] LineHashes)>(StringComparer.Ordinal)
        {
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-blocker-repair-20261014/tooling-before/tests/test_p1r_published_executor.py"] =
                ("e7cb8298ada4b1cb159076411ae19158505b0118119bbf9c86579b53e3a71256", ["f83d792143633f7793983b1f64aa87da3525eb180635a51f5a2a995a13b3597a"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-blocker-repair-20261014/tooling-before/tests/test_p1r_published_qualification.py"] =
                ("d607a04a79b4a24a8f84aea2f6e00d3c7e7f474ef51cedf235dcd8dc8ca0bc9d", ["1524aab9822def7d5396ffe7fe461e8ffd75d6be1d8c8c98a4b26b96687f9c52"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-canonical-reconciliation-20261016/pre-canonical-refresh-reviewed-reconciliation.diff"] =
                ("396b80abbf05cd56a86b065910919c69e5fb73d1cf63221860c216a66123915e", ["e83be9bbd57c3b2f94ee9e576f717e12547fc32614250fa233e640c18ca9da0b", "eab7e2ab144bb388112b69ce2c630bca8a7da721561b06465081899fb8ac1629"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-canonical-reconciliation-20261016/review-v1.diff"] =
                ("1e42232b1cdeb7637b2cd628a0822ddd527bdfde12d263955f11934665231c22", ["e83be9bbd57c3b2f94ee9e576f717e12547fc32614250fa233e640c18ca9da0b", "c413772a6a4011640f87eeaa2f0ad181f2c3e52d1650d57aca9a261d12ba4947", "e7e43ebc36b5e3fb7ac50a4b10bbbde4fe5e43d98fdc6bf11b7d87cae866379e"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-canonical-reconciliation-20261016/reviewed-reconciliation.diff"] =
                ("006a4bfee5d3e3e9a0eeef8f4a2f23eadf81a567bf99ca9541f5e5517b84b603", ["e83be9bbd57c3b2f94ee9e576f717e12547fc32614250fa233e640c18ca9da0b", "eab7e2ab144bb388112b69ce2c630bca8a7da721561b06465081899fb8ac1629"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-successor-enrollment-20261014/release-analysis/bexec-preflight.txt"] =
                ("f87b48e649092cc68de8f07977bae48c65f0570007f60de42887201988ee0062", ["b97b4ce96a582dcb5ed66aec4f94f4f7a57fb647323aeec81f1f903583c1920a"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-successor-enrollment-20261014/release-analysis/canonical-ci-workflow.txt"] =
                ("1388d7b2f4cf96d01da3f078c09c975768c89ac79e14ef649635b7c2f79a0ae7", ["c5b6e30a9485fed934bb3e037506a78bb2dfa6c0098610d3ff88d859c64e0b58"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-successor-enrollment-20261014/release-analysis/canonical-p1r-workflow.txt"] =
                ("d88063ae47f84b68d362ac8c985d0ed00809a48c387de28c5801418875a59885", ["c5b6e30a9485fed934bb3e037506a78bb2dfa6c0098610d3ff88d859c64e0b58"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-successor-enrollment-20261014/release-analysis/canonical-release-workflow.txt"] =
                ("91f7b28d0ae1e805492af3a937849bafee0f071809f4757024bf380cc6bac31b", ["c5b6e30a9485fed934bb3e037506a78bb2dfa6c0098610d3ff88d859c64e0b58"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-tooling-hardening-20261016/independent-review.md"] =
                ("e489cc6114a99ae99fc38512813bd08441df1ccb566d78744fa114f50b4f3152", ["96fc74e6f25bfc03f70b220eb0332e9332bc731734490e7339279d71cea1965a"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-tooling-hardening-20261016/review-v1.diff"] =
                ("616eb2598715ebb58a3f8c2a8f2f04e0abcc83f92ffee24ad6ee20ed6da7e6ec", ["0734eac0889dd0867a981e9f11268fa64ddead733bfd613570f25bd826999306"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-tooling-hardening-20261016/review-v2.diff"] =
                ("3868c28072efd3fc34213697beaec76ca6190d52a15ac65a6f66030eaac6e7c8", ["c413772a6a4011640f87eeaa2f0ad181f2c3e52d1650d57aca9a261d12ba4947", "e7e43ebc36b5e3fb7ac50a4b10bbbde4fe5e43d98fdc6bf11b7d87cae866379e"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-tooling-hardening-20261016/review-v3.diff"] =
                ("8e7abfc544afc463dd853b3ae7882ad6eb3a76978dad5fe0d0060ad82024947d", ["c413772a6a4011640f87eeaa2f0ad181f2c3e52d1650d57aca9a261d12ba4947", "e7e43ebc36b5e3fb7ac50a4b10bbbde4fe5e43d98fdc6bf11b7d87cae866379e"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-tooling-hardening-20261016/review-v4.diff"] =
                ("0141061615e01e4aba0ad1e117181474e7b6e8d9dcc7eca71ae489ed9f450f92", ["c413772a6a4011640f87eeaa2f0ad181f2c3e52d1650d57aca9a261d12ba4947", "e7e43ebc36b5e3fb7ac50a4b10bbbde4fe5e43d98fdc6bf11b7d87cae866379e"]),
            // Reviewed immutable capture: captured-xunit-fixture-metadata.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-current-source-20261022T134028Z/admin-abstractions-full.xml"] =
                ("24ff8682147684ef179ca36e1e94a42e860aba919a05ea3c36e5e16630fcc9e5", ["24ff8682147684ef179ca36e1e94a42e860aba919a05ea3c36e5e16630fcc9e5"]),
            // Reviewed immutable capture: captured-xunit-fixture-metadata.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-current-source-20261022T134028Z/admin-cli-full.xml"] =
                ("b20227b89170b006af32facf434c5f8326e1cd36aa1db4fb52d2a9139c97b0bb", ["b20227b89170b006af32facf434c5f8326e1cd36aa1db4fb52d2a9139c97b0bb"]),
            // Reviewed immutable capture: captured-xunit-fixture-metadata.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-current-source-20261022T134028Z/admin-mcp-full.xml"] =
                ("dff2b7695950172d4bbc72b4dea384ffd5d6c8d5b99ad50e83620688ca7d2dd5", ["dff2b7695950172d4bbc72b4dea384ffd5d6c8d5b99ad50e83620688ca7d2dd5"]),
            // Reviewed immutable capture: captured-xunit-fixture-metadata.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-current-source-20261022T134028Z/admin-ui-full.xml"] =
                ("a960b8da4f535be75ac626e1fcada3a91dcafa42a3c8c90d58ed7c2671ba27d9", ["a960b8da4f535be75ac626e1fcada3a91dcafa42a3c8c90d58ed7c2671ba27d9"]),
            // Reviewed immutable capture: captured-test-display-name.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-current-source-20261022T134028Z/affected-server-regressions.json"] =
                ("f75eadaa03e4aa5689d470c206426575e14569a9b167180e0ed9cc711b302012", ["092f4b97aeadfe91213762ee5a21c845b9952903787882e12558df190e9ff47e"]),
            // Reviewed immutable capture: captured-xunit-fixture-metadata.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-current-source-20261022T134028Z/apphost-full.xml"] =
                ("9e3a502f4e8062b205ad9d3b776382a5a7dd9bd48614f09dca458282a5cb9f87", ["9e3a502f4e8062b205ad9d3b776382a5a7dd9bd48614f09dca458282a5cb9f87"]),
            // Reviewed immutable capture: captured-test-authentication-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-current-source-20261022T134028Z/bound-source/3952489c051d7b10820ab765c9de1692d239885288a96b0b5d4728bea678ea54.source"] =
                ("3952489c051d7b10820ab765c9de1692d239885288a96b0b5d4728bea678ea54", ["e0bca6a316885d9cc8efe45f6a805236f55d93caf8d95e6da4268ed8503ffb27"]),
            // Reviewed immutable capture: captured-test-authentication-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-current-source-20261022T134028Z/bound-source/8f109f47db9be0915b2d38dec31f4f2326e4b4449e86bbbf3247f5f5ba9c3e50.source"] =
                ("8f109f47db9be0915b2d38dec31f4f2326e4b4449e86bbbf3247f5f5ba9c3e50", ["6f32033f44a8af99c3eacabbff6eab4c7d9afa8471177fd9345c73df621636ed"]),
            // Reviewed immutable capture: noncredential-governance-protocol-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-current-source-20261022T134028Z/bound-source/c9b0b22524975ebf28a88fb0acffa669bd0ded22f1e00199663a7515304ab656.source"] =
                ("c9b0b22524975ebf28a88fb0acffa669bd0ded22f1e00199663a7515304ab656", ["1e9da006eae451aed04bf56dfe8e194b39b84e1a896a6aafacd8610b8088320d"]),
            // Reviewed immutable capture: captured-xunit-fixture-metadata.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-current-source-20261022T134028Z/sample-full.xml"] =
                ("e67851df06f3db6405d72a9baab222f2ee5e7a44eedb4ec6de661d99c5a3330b", ["e67851df06f3db6405d72a9baab222f2ee5e7a44eedb4ec6de661d99c5a3330b"]),
            // Reviewed immutable capture: captured-xunit-fixture-metadata.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-current-source-20261022T134028Z/server-full.xml"] =
                ("9ea5fa7e36d61d32d298343c2e5f217e043fd01e898530d5e3424db2a31be326", ["7b6ca06ef8806fbfd1808da8fe1b5394931b6369ab449dfeea3685baf2721729"]),
            // Reviewed immutable capture: captured-test-display-name.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-gates-20261022T151209Z/affected-server-regressions.json"] =
                ("13a7eba218fce610f80268827cdebeea80dabca0d98256911d2d541197031a9f", ["092f4b97aeadfe91213762ee5a21c845b9952903787882e12558df190e9ff47e"]),
            // Reviewed immutable capture: noncredential-governance-protocol-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-gates-20261022T151209Z/bound-source/c9b0b22524975ebf28a88fb0acffa669bd0ded22f1e00199663a7515304ab656.cs"] =
                ("c9b0b22524975ebf28a88fb0acffa669bd0ded22f1e00199663a7515304ab656", ["1e9da006eae451aed04bf56dfe8e194b39b84e1a896a6aafacd8610b8088320d"]),
            // Reviewed immutable capture: captured-xunit-fixture-metadata.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-gates-20261022T151209Z/server-full.xml"] =
                ("7fa37a7a2556d8dc69f8aaa4761383b5f8f5d0fa59f9e5b33b834e7ab3668e31", ["56ff88519b0829cb747c916607c8d6e6e87368fc21aa46cd6d5c02977bce5649"]),
            // Reviewed immutable capture: captured-test-display-name.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/affected-server-regressions.json"] =
                ("ddf506ec23035e85e91277d639d3dc85ca7a432bae73038058b52698e32370c7", ["092f4b97aeadfe91213762ee5a21c845b9952903787882e12558df190e9ff47e"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture, captured-test-authentication-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/bound-source/452c23b865a4f28a9f2bf9ac66f307881fb2882a49b9be20db91ce875b35eb9e.cs"] =
                ("452c23b865a4f28a9f2bf9ac66f307881fb2882a49b9be20db91ce875b35eb9e", ["970be605359b9e62a344f6b60767a42d0f52f6095b13b5c37d84bd8fff42297e", "59770f1ddbd76a6cc6be248617f5d826a7d5817eb1e57787a395fc02038130bf", "d239c8fd3d2c3222f5a66756b8cbcfd9caf38b53c35af4a646912ef150d40019"]),
            // Reviewed immutable capture: noncredential-governance-protocol-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/bound-source/c9b0b22524975ebf28a88fb0acffa669bd0ded22f1e00199663a7515304ab656.cs"] =
                ("c9b0b22524975ebf28a88fb0acffa669bd0ded22f1e00199663a7515304ab656", ["1e9da006eae451aed04bf56dfe8e194b39b84e1a896a6aafacd8610b8088320d"]),
            // Reviewed immutable capture: captured-scanner-regression-diff.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-final/input-identity-audit.json"] =
                ("284e3b83bc2c58f5cf611077fe7e73e9c35ff055d83051694cdd68e77df34299", ["b0fa40b9c5025097fb4d9291b1d34a075491008792ddf9e1f5af37f87838942c"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-final/origin-before-SecretsProtectionTests.UnifiedDiffCursor.cs.source"] =
                ("1fe49de76f60460f3ada1ddd4436f5cbf142a1c1ec92b230289e1ae0dc289555", ["d403f07d7cc30189e26dbef1c88143fea6907b978ea2a6870f9f0ff856f34b17"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-final/origin-change-SecretsProtectionTests.UnifiedDiffCursor.cs.patch"] =
                ("efaa9fec5d8b54474415ed7d0a19335b6f2e12fcf11b6e8ef95790c2f4254c99", ["df7b46116f896d631610185b4527f74a5f7602afbceb21f7a6cc8726a9e1214e"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-final/snapshot-before-SecretsProtectionTests.UnifiedDiffCursor.cs.source"] =
                ("1fe49de76f60460f3ada1ddd4436f5cbf142a1c1ec92b230289e1ae0dc289555", ["d403f07d7cc30189e26dbef1c88143fea6907b978ea2a6870f9f0ff856f34b17"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-final/snapshot-change-SecretsProtectionTests.UnifiedDiffCursor.cs.patch"] =
                ("eaa83daa23628e697087ee603f8c2488d4dc3b361208ec5a324bd791f88df4c1", ["df7b46116f896d631610185b4527f74a5f7602afbceb21f7a6cc8726a9e1214e"]),
            // Reviewed immutable capture: captured-scanner-regression-diff.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-fixtures/input-identity-audit.json"] =
                ("ee18aaa3fba5ab5fec2ce670e27a0a5edb1bf872e1e7c24d55c75ab20b6ae447", ["24330120599f44354712979eb302dd9bba7868435fa15e3dc096019364fda617"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-fixtures/origin-before.cs.source"] =
                ("1307cd9a0b5702c3c42f0eadd3b251b70cc1b3a0a4427165040a0ed84551cf40", ["970be605359b9e62a344f6b60767a42d0f52f6095b13b5c37d84bd8fff42297e", "59770f1ddbd76a6cc6be248617f5d826a7d5817eb1e57787a395fc02038130bf"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-fixtures/origin-change.patch"] =
                ("283160e4bbc32129ded433c6b561dcbc9cdfa5517cbf875b8a925afc479ae94a", ["fd079742df96ee469b7ef333797d27098ba98ac303ddbed737a10e1eec97dbbf", "93ed0cd31cb1287404755b6a80ae01d490f4bf9698bd2bd0a63fd49a621a2758"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-fixtures/runtime/SecretsProtectionTests.UnifiedDiffCursor.cs.source"] =
                ("1fe49de76f60460f3ada1ddd4436f5cbf142a1c1ec92b230289e1ae0dc289555", ["d403f07d7cc30189e26dbef1c88143fea6907b978ea2a6870f9f0ff856f34b17"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-fixtures/snapshot-before.cs.source"] =
                ("0b64b237a633d87eb95dea5b727b2c24ffff51929c906080082d5fabbbfb8c68", ["970be605359b9e62a344f6b60767a42d0f52f6095b13b5c37d84bd8fff42297e", "59770f1ddbd76a6cc6be248617f5d826a7d5817eb1e57787a395fc02038130bf"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-fixtures/snapshot-change.patch"] =
                ("6ad8f57fc4850632bee583697214df34db12f4ac867545eae47aa51912ca34d4", ["fd079742df96ee469b7ef333797d27098ba98ac303ddbed737a10e1eec97dbbf", "93ed0cd31cb1287404755b6a80ae01d490f4bf9698bd2bd0a63fd49a621a2758"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-repair/new-regressions-v2.cs.source"] =
                ("1fe49de76f60460f3ada1ddd4436f5cbf142a1c1ec92b230289e1ae0dc289555", ["d403f07d7cc30189e26dbef1c88143fea6907b978ea2a6870f9f0ff856f34b17"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-repair/new-regressions.cs.source"] =
                ("d2da413d9cd1a2898eb675bd793edef0565256d83359b12e17ceb0c81c90e4e8", ["d403f07d7cc30189e26dbef1c88143fea6907b978ea2a6870f9f0ff856f34b17"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture, captured-test-authentication-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-repair/origin-after.cs.source"] =
                ("301fde6e37c56a499138dc6ba7dd753038d39d39f718872bd2325d2c846be180", ["970be605359b9e62a344f6b60767a42d0f52f6095b13b5c37d84bd8fff42297e", "59770f1ddbd76a6cc6be248617f5d826a7d5817eb1e57787a395fc02038130bf", "d239c8fd3d2c3222f5a66756b8cbcfd9caf38b53c35af4a646912ef150d40019"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture, captured-test-authentication-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-repair/origin-before.cs.source"] =
                ("5679c4995b5019aa02b3fa2decc77c1d38c5d5ff6c42eca97a2a715fdf17233a", ["970be605359b9e62a344f6b60767a42d0f52f6095b13b5c37d84bd8fff42297e", "59770f1ddbd76a6cc6be248617f5d826a7d5817eb1e57787a395fc02038130bf", "d239c8fd3d2c3222f5a66756b8cbcfd9caf38b53c35af4a646912ef150d40019"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-repair/runtime/SecretsProtectionTests.UnifiedDiffCursor.cs.source"] =
                ("1fe49de76f60460f3ada1ddd4436f5cbf142a1c1ec92b230289e1ae0dc289555", ["d403f07d7cc30189e26dbef1c88143fea6907b978ea2a6870f9f0ff856f34b17"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture, captured-test-authentication-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-repair/runtime/SecretsProtectionTests.cs.source"] =
                ("22230ab29679672082431dba0160c6caaa265fc50a32c1f2884f3def25d86ef8", ["970be605359b9e62a344f6b60767a42d0f52f6095b13b5c37d84bd8fff42297e", "59770f1ddbd76a6cc6be248617f5d826a7d5817eb1e57787a395fc02038130bf", "d239c8fd3d2c3222f5a66756b8cbcfd9caf38b53c35af4a646912ef150d40019"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture, captured-test-authentication-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-repair/snapshot-after.cs.source"] =
                ("22230ab29679672082431dba0160c6caaa265fc50a32c1f2884f3def25d86ef8", ["970be605359b9e62a344f6b60767a42d0f52f6095b13b5c37d84bd8fff42297e", "59770f1ddbd76a6cc6be248617f5d826a7d5817eb1e57787a395fc02038130bf", "d239c8fd3d2c3222f5a66756b8cbcfd9caf38b53c35af4a646912ef150d40019"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture, captured-test-authentication-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-repair/snapshot-before.cs.source"] =
                ("452c23b865a4f28a9f2bf9ac66f307881fb2882a49b9be20db91ce875b35eb9e", ["970be605359b9e62a344f6b60767a42d0f52f6095b13b5c37d84bd8fff42297e", "59770f1ddbd76a6cc6be248617f5d826a7d5817eb1e57787a395fc02038130bf", "d239c8fd3d2c3222f5a66756b8cbcfd9caf38b53c35af4a646912ef150d40019"]),
            // Reviewed immutable capture: captured-scanner-regression-diff.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-tightening/input-identity-audit.json"] =
                ("e2c4378636d7760c2c8891993baf8427e42d9e98aef169ae7c8a3e5d02d27457", ["5a00ecd5ac42a7523acd6d8691bfb4a92f21e9dc4327b5aa6ad072c0841d3c0e"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture, captured-test-authentication-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-tightening/origin-after.cs.source"] =
                ("ad1671b2e00671f73cabec07ea3e8fa7482809f39629e23210ce5454f584be4c", ["970be605359b9e62a344f6b60767a42d0f52f6095b13b5c37d84bd8fff42297e", "59770f1ddbd76a6cc6be248617f5d826a7d5817eb1e57787a395fc02038130bf", "d239c8fd3d2c3222f5a66756b8cbcfd9caf38b53c35af4a646912ef150d40019"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture, captured-test-authentication-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-tightening/origin-before.cs.source"] =
                ("31defd41157f833beffb87e196211b1b21478ae4ab64f6f84c202bd48e2e1119", ["970be605359b9e62a344f6b60767a42d0f52f6095b13b5c37d84bd8fff42297e", "59770f1ddbd76a6cc6be248617f5d826a7d5817eb1e57787a395fc02038130bf", "d239c8fd3d2c3222f5a66756b8cbcfd9caf38b53c35af4a646912ef150d40019"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-tightening/origin-final.cs.source"] =
                ("1307cd9a0b5702c3c42f0eadd3b251b70cc1b3a0a4427165040a0ed84551cf40", ["970be605359b9e62a344f6b60767a42d0f52f6095b13b5c37d84bd8fff42297e", "59770f1ddbd76a6cc6be248617f5d826a7d5817eb1e57787a395fc02038130bf"]),
            // Reviewed immutable capture: captured-test-authentication-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-tightening/origin-fixture-change.patch"] =
                ("49155783cefad4957b5e264570ae9eda2f4d7a9ee7261239d3cc7131438126b9", ["0bbdfa417b5f44d47f1d7bccff8c11011ec9e783a72144177ab609a6a541877f"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-tightening/runtime/SecretsProtectionTests.UnifiedDiffCursor.cs.source"] =
                ("1fe49de76f60460f3ada1ddd4436f5cbf142a1c1ec92b230289e1ae0dc289555", ["d403f07d7cc30189e26dbef1c88143fea6907b978ea2a6870f9f0ff856f34b17"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-tightening/runtime/SecretsProtectionTests.cs.source"] =
                ("0b64b237a633d87eb95dea5b727b2c24ffff51929c906080082d5fabbbfb8c68", ["970be605359b9e62a344f6b60767a42d0f52f6095b13b5c37d84bd8fff42297e", "59770f1ddbd76a6cc6be248617f5d826a7d5817eb1e57787a395fc02038130bf"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture, captured-test-authentication-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-tightening/snapshot-after.cs.source"] =
                ("58caa124c32e194224d216c8d8fe90c80d0e139619e0252a1e3b354f131e5aa5", ["970be605359b9e62a344f6b60767a42d0f52f6095b13b5c37d84bd8fff42297e", "59770f1ddbd76a6cc6be248617f5d826a7d5817eb1e57787a395fc02038130bf", "d239c8fd3d2c3222f5a66756b8cbcfd9caf38b53c35af4a646912ef150d40019"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture, captured-test-authentication-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-tightening/snapshot-before.cs.source"] =
                ("22230ab29679672082431dba0160c6caaa265fc50a32c1f2884f3def25d86ef8", ["970be605359b9e62a344f6b60767a42d0f52f6095b13b5c37d84bd8fff42297e", "59770f1ddbd76a6cc6be248617f5d826a7d5817eb1e57787a395fc02038130bf", "d239c8fd3d2c3222f5a66756b8cbcfd9caf38b53c35af4a646912ef150d40019"]),
            // Reviewed immutable capture: captured-scanner-runtime-expression-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-tightening/snapshot-final.cs.source"] =
                ("0b64b237a633d87eb95dea5b727b2c24ffff51929c906080082d5fabbbfb8c68", ["970be605359b9e62a344f6b60767a42d0f52f6095b13b5c37d84bd8fff42297e", "59770f1ddbd76a6cc6be248617f5d826a7d5817eb1e57787a395fc02038130bf"]),
            // Reviewed immutable capture: captured-test-authentication-fixture.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-tightening/snapshot-fixture-change.patch"] =
                ("901af5fe4c8da6b1472874f102db8a2cbd6a2fbc9f4907fdcf967a41ba0a18a4", ["0bbdfa417b5f44d47f1d7bccff8c11011ec9e783a72144177ab609a6a541877f"]),
            // Reviewed immutable capture: captured-xunit-fixture-metadata.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guardfinal-server-full.xml"] =
                ("1e57af202caa98af3cc7a36c67645e4384ab6a81fb02a78ef9185c892b9b1e8c", ["1810db8346456d025e638f4d2f39706058397bff78b9db990740663d78c2c6ef"]),
            // Reviewed immutable capture: captured-xunit-fixture-metadata.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guardfix-server-full.xml"] =
                ("e69d11db5eae61432181b4df36e9c4807ee308f50a9d552ff3581f6c40522f3b", ["0cc78c3316c6d213c73de2844c27dc60b2ce536918119ec6c480dfdbb63f0bfb"]),
            // Reviewed immutable capture: captured-xunit-fixture-metadata.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guardfixtures-server-full.xml"] =
                ("75edd766adf9497495af0aa7fd81baa73bb911a7e544db13c34cecd476d83d89", ["29f29f7695b2e42fda10de6d5ae659684e853423e11a8c511bbc3392f9dcd436"]),
            // Reviewed immutable capture: captured-xunit-fixture-metadata.
            ["_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guardtight-server-full.xml"] =
                ("3280648528953b1937fbc0cff465f52a5b6b2c707af322f366de50b1e901df9d", ["0467ceb8d7c5dc1e3ce664be18262b11d78defa4995fc7afcfd1d9e6b4f757f9"]),
            // Reviewed immutable capture: runtime-generated-python-signing-key, captured-test-authentication-fixture.
            ["_bmad-output/implementation-artifacts/evidence/internal-actor-admission-20261022/final-source.patch"] =
                ("cab06008db1f71bcd601f330d61582bb111a10c252e20f9d998f596d7d0c257c", ["8de4f803eb5d639eabda92072f97b0afcbfa939e0c3d96b404914138d72c3a50", "c5db449074b7eeed89deef4b2ba8bc05327abf570d28576ccaf817d54936039e"]),
            // Reviewed immutable capture: runtime-generated-python-signing-key.
            ["_bmad-output/implementation-artifacts/evidence/internal-actor-admission-20261022/review-source.patch"] =
                ("e0ef530005303f8c4729afeecadc70491462254a547fd6d612c66e58f1d6891b", ["8de4f803eb5d639eabda92072f97b0afcbfa939e0c3d96b404914138d72c3a50"]),
        };

    /// <summary>
    /// Verifies catalog counts and JSON structure are not usable secrets, while real literals remain violations.
    /// </summary>
    [Fact]
    public void QualifiedCountAndStructuralJsonValues_AreNotUsableSecrets()
    {
        const string path = "evidence/catalog.json";
        FindViolations(
            path,
            "\"Hexalith.Tests.RequiresCredentials\": 128,\n\"oci_credentials\": {\n")
            .ShouldBeEmpty();
        FindViolations(path, "api.token: " + new string('9', 9)).ShouldNotBeEmpty();
        FindViolations(path, Assignment("password", RandomSecret())).ShouldNotBeEmpty();
    }

    /// <summary>
    /// Verifies sealed historical evidence stays quiet only while its bytes match, and a changed copy is scanned.
    /// </summary>
    [Fact]
    public void SealedHistoricalSecretLines_RequireExactBytes()
    {
        foreach (string path in SealedHistoricalSecretFiles.Keys)
        {
            ScanTrackedFile(path).ShouldBeEmpty($"Sealed historical evidence changed: {path}");
        }

        string sample = "_bmad-output/implementation-artifacts/evidence/6-1-p1r-successor-enrollment-20261014/release-analysis/canonical-p1r-workflow.txt";
        WithEvidenceRepository(root =>
        {
            byte[] original = File.ReadAllBytes(Path.Combine(RepoRoot, sample));
            WriteEvidenceFile(root, sample, original);
            ScanTrackedFile(sample, root).ShouldBeEmpty();
            WriteEvidenceFile(root, sample, [..original, (byte)'\n']);
            ScanTrackedFile(sample, root).ShouldNotBeEmpty();
        });
    }

    /// <summary>Every reviewed capture category revokes its seal on changes and detects adjacent credentials.</summary>
    /// <param name="path">The exact historical capture path.</param>
    /// <param name="lineNumber">The reviewed inert line used for the same-line mutation.</param>
    [Theory]
    [InlineData("_bmad-output/implementation-artifacts/evidence/folders-12-2-current-source-20261022T134028Z/admin-abstractions-full.xml", 1)]
    [InlineData("_bmad-output/implementation-artifacts/evidence/folders-12-2-current-source-20261022T134028Z/affected-server-regressions.json", 1052)]
    [InlineData("_bmad-output/implementation-artifacts/evidence/folders-12-2-current-source-20261022T134028Z/bound-source/3952489c051d7b10820ab765c9de1692d239885288a96b0b5d4728bea678ea54.source", 36)]
    [InlineData("_bmad-output/implementation-artifacts/evidence/folders-12-2-current-source-20261022T134028Z/bound-source/c9b0b22524975ebf28a88fb0acffa669bd0ded22f1e00199663a7515304ab656.source", 42)]
    [InlineData("_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/bound-source/452c23b865a4f28a9f2bf9ac66f307881fb2882a49b9be20db91ce875b35eb9e.cs", 173)]
    [InlineData("_bmad-output/implementation-artifacts/evidence/folders-12-2-latest-20261022T155454Z/guard-final/input-identity-audit.json", 116)]
    [InlineData("_bmad-output/implementation-artifacts/evidence/internal-actor-admission-20261022/final-source.patch", 660)]
    public void SealedReleaseCaptures_ChangesAndAdjacentCredentialsRemainScanned(string path, int lineNumber)
    {
        ArgumentNullException.ThrowIfNull(path);
        WithEvidenceRepository(root =>
        {
            byte[] original = File.ReadAllBytes(Path.Combine(RepoRoot, path));
            WriteEvidenceFile(root, path, original);
            ScanTrackedFile(path, root).ShouldBeEmpty();

            WriteEvidenceFile(root, path, [.. original, (byte)'\n']);
            ScanTrackedFile(path, root).ShouldNotBeEmpty("Any changed byte must revoke the historical seal.");

            string text = Encoding.UTF8.GetString(original);
            string probe = Assignment("pass" + "word", JsonSerializer.Serialize(RandomSecret()));
            string[] lines = text.Split('\n');
            lines[lineNumber - 1] = lines[lineNumber - 1].TrimEnd('\r') + " " + probe;
            WriteEvidenceFile(root, path, Encoding.UTF8.GetBytes(string.Join('\n', lines)));
            ScanTrackedFile(path, root).ShouldContain(path + ":" + lineNumber);

            // Remove the original trigger so this line can only be reported for the injected credential.
            string framing = path.EndsWith(".patch", StringComparison.Ordinal)
                ? text.Split('\n')[lineNumber - 1][..1] : string.Empty;
            lines[lineNumber - 1] = framing;
            WriteEvidenceFile(root, path, Encoding.UTF8.GetBytes(string.Join('\n', lines)));
            ScanTrackedFile(path, root).ShouldNotContain(path + ":" + lineNumber);
            lines[lineNumber - 1] += probe;
            WriteEvidenceFile(root, path, Encoding.UTF8.GetBytes(string.Join('\n', lines)));
            ScanTrackedFile(path, root).ShouldContain(path + ":" + lineNumber);

            string adjacent = text.TrimEnd('\r', '\n') + "\n" + probe;
            WriteEvidenceFile(root, path, Encoding.UTF8.GetBytes(adjacent));
            ScanTrackedFile(path, root).ShouldContain(path + ":" + (adjacent.Count(character => character == '\n') + 1));
        });
    }

    private static IEnumerable<string> SuppressSealedHistoricalSecretLines(
        string path,
        string content,
        string? repositoryRoot,
        IEnumerable<string> violations)
    {
        if (!SealedHistoricalSecretFiles.TryGetValue(path, out (string Sha256, string[] LineHashes) seal))
        {
            return violations;
        }

        string root = repositoryRoot ?? RepoRoot;
        string actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, path))))
            .ToLowerInvariant();
        if (!string.Equals(actualHash, seal.Sha256, StringComparison.Ordinal))
        {
            return violations;
        }

        string[] lines = content.Split('\n');
        HashSet<string> sealedLines = new(seal.LineHashes, StringComparer.Ordinal);
        return violations.Where(violation => !IsSealedHistoricalLine(path, lines, sealedLines, violation));
    }

    private static bool IsSealedHistoricalLine(string path, string[] lines, HashSet<string> sealedLines, string violation)
    {
        string prefix = path + ":";
        if (!violation.StartsWith(prefix, StringComparison.Ordinal)
            || !int.TryParse(violation[prefix.Length..], out int lineNumber)
            || lineNumber < 1
            || lineNumber > lines.Length)
        {
            return false;
        }

        string line = lines[lineNumber - 1].TrimEnd('\r');
        string lineHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(line))).ToLowerInvariant();
        return sealedLines.Contains(lineHash);
    }
}
