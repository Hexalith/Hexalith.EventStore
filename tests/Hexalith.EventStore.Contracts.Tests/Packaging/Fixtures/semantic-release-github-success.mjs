import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createServer } from "node:http";
import { createRequire } from "node:module";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { success as githubSuccess } from "@semantic-release/github";

const fixtureDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(fixtureDirectory, "../../../..");
const releaseConfiguration = JSON.parse(
    await readFile(resolve(repositoryRoot, ".releaserc.json"), "utf8"),
);
const packageLock = JSON.parse(
    await readFile(resolve(repositoryRoot, "package-lock.json"), "utf8"),
);
const githubPlugin = releaseConfiguration.plugins.find(
    (plugin) =>
        Array.isArray(plugin) && plugin[0] === "@semantic-release/github",
);

assert.ok(
    githubPlugin,
    "The GitHub semantic-release plugin must be configured.",
);
const githubOptions = githubPlugin[1];
assert.deepEqual(githubOptions.assets, ["nupkgs/*.nupkg"]);
assert.equal(githubOptions.successCommentCondition, false);
assert.equal(Object.hasOwn(githubOptions, "successComment"), false);
assert.equal(Object.hasOwn(githubOptions, "failCommentCondition"), false);

const lockedPackage =
    packageLock.packages?.["node_modules/@semantic-release/github"];
assert.ok(
    lockedPackage?.version,
    "package-lock.json must pin @semantic-release/github.",
);
const require = createRequire(import.meta.url);
const installedEntry = require.resolve("@semantic-release/github");
const installedPackage = JSON.parse(
    await readFile(resolve(dirname(installedEntry), "package.json"), "utf8"),
);
assert.equal(installedPackage.name, "@semantic-release/github");
assert.equal(
    installedPackage.version,
    lockedPackage.version,
    "The installed GitHub plugin must match package-lock.json.",
);

const repository = "Hexalith/Hexalith.EventStore";
const repositoryPath = "/repos/Hexalith/Hexalith.EventStore";
const repositoryUrl = `https://github.com/${repository}.git`;
const runIds = ["29738838856", "29720431798"];
const requests = [];
const forbiddenRequests = [];

const respondJson = (response, status, value) => {
    response.writeHead(status, { "content-type": "application/json" });
    response.end(JSON.stringify(value));
};

const readRequestBody = async (request) => {
    const chunks = [];
    for await (const chunk of request) {
        chunks.push(chunk);
    }

    return Buffer.concat(chunks).toString("utf8");
};

const server = createServer(async (request, response) => {
    const method = request.method ?? "UNKNOWN";
    const pathname = new URL(request.url ?? "/", "http://fixture.invalid")
        .pathname;
    const body = await readRequestBody(request);
    const record = { body, method, pathname };
    requests.push(record);

    const remoteAddress = request.socket.remoteAddress ?? "";
    const isLoopbackPeer = ["127.0.0.1", "::1", "::ffff:127.0.0.1"].includes(
        remoteAddress,
    );
    if (!isLoopbackPeer) {
        forbiddenRequests.push(
            `${method} ${pathname}: non-loopback peer ${remoteAddress}`,
        );
        respondJson(response, 418, { message: "Non-loopback fixture request" });
        return;
    }

    if (method === "GET" && pathname === repositoryPath && body.length === 0) {
        respondJson(response, 200, {
            clone_url: repositoryUrl,
            default_branch: "main",
            full_name: repository,
            permissions: { push: true },
        });
        return;
    }

    if (method === "POST" && pathname.endsWith("/graphql")) {
        let graphql;
        try {
            graphql = JSON.parse(body);
        } catch {
            forbiddenRequests.push(`${method} ${pathname}: invalid JSON body`);
            respondJson(response, 418, {
                message: "Invalid GraphQL fixture request",
            });
            return;
        }

        const serializedRequest = JSON.stringify(graphql);
        const query = typeof graphql.query === "string" ? graphql.query : "";
        const containsRunId = runIds.some((runId) =>
            serializedRequest.includes(runId),
        );
        const isStaleFailureCleanup = /\bquery\s+getSRIssues\b/.test(query);
        const isReferenceResolution =
            /associatedPullRequests|associatedPR|relatedIssues|relatedPR/i.test(
                query,
            );

        if (containsRunId || isReferenceResolution || !isStaleFailureCleanup) {
            forbiddenRequests.push(
                `${method} ${pathname}: forbidden GraphQL operation`,
            );
            respondJson(response, 418, {
                message: "Forbidden GraphQL fixture request",
            });
            return;
        }

        respondJson(response, 200, {
            data: { repository: { issues: { nodes: [] } } },
        });
        return;
    }

    const isNumericNotificationMutation =
        ["DELETE", "PATCH", "POST", "PUT"].includes(method) &&
        /\/(?:issues|pulls)\/\d+(?:\/(?:comments|labels))?$/.test(pathname);
    const reason = isNumericNotificationMutation
        ? "numeric comment or label mutation"
        : "unexpected endpoint";
    forbiddenRequests.push(`${method} ${pathname}: ${reason}`);
    respondJson(response, 418, {
        message: `Forbidden fixture request: ${reason}`,
    });
});

await new Promise((resolveListen, rejectListen) => {
    server.once("error", rejectListen);
    server.listen(0, "127.0.0.1", resolveListen);
});

const address = server.address();
assert.ok(address && typeof address === "object");
const githubApiUrl = `http://127.0.0.1:${address.port}`;
assert.equal(new URL(githubApiUrl).hostname, "127.0.0.1");

const histories = [
    {
        name: "issue-like merge history",
        commits: [
            {
                hash: "a".repeat(40),
                message:
                    "build: merge fix/gh-29738838856-live-sidecar-postgres-pull into main via /pushall",
            },
            {
                hash: "b".repeat(40),
                message:
                    "build: merge fix/gh-29720431798-release-pin into main via /pushall",
            },
        ],
    },
    {
        name: "ordinary conventional history",
        commits: [
            {
                hash: "c".repeat(40),
                message: "fix(server): preserve event sequence ordering",
            },
        ],
    },
];
const logger = {
    error() {},
    log() {},
    warn() {},
};
const nativeFetch = globalThis.fetch;
let executionError;

globalThis.fetch = async (input, init) => {
    const target = new URL(
        typeof input === "string" || input instanceof URL ? input : input.url,
    );
    if (
        target.hostname !== "127.0.0.1" ||
        target.port !== String(address.port)
    ) {
        const record = `non-loopback fetch ${target.href}`;
        forbiddenRequests.push(record);
        throw new Error(record);
    }

    return nativeFetch(input, init);
};

try {
    for (const history of histories) {
        const requestCountBeforeHistory = requests.length;
        await githubSuccess(
            {
                ...githubOptions,
                githubApiUrl,
            },
            {
                branch: { name: "main" },
                commits: history.commits,
                env: {
                    GITHUB_ACTION: "true",
                    GITHUB_TOKEN: "fixture-token",
                },
                logger,
                nextRelease: {
                    gitTag: "v99.0.0",
                    notes: "Fixture release notes",
                    version: "99.0.0",
                },
                options: { repositoryUrl },
                releases: [],
            },
        );

        const historyRequests = requests.slice(requestCountBeforeHistory);
        assert.ok(
            historyRequests.some(
                (request) =>
                    request.method === "GET" &&
                    request.pathname === repositoryPath,
            ),
            `${history.name} must read repository metadata through loopback.`,
        );
        assert.equal(
            historyRequests.filter(
                (request) =>
                    request.method === "POST" &&
                    request.pathname.endsWith("/graphql") &&
                    /\bquery\s+getSRIssues\b/.test(
                        JSON.parse(request.body).query,
                    ),
            ).length,
            1,
            `${history.name} must perform only the stale-failure cleanup query.`,
        );
    }
} catch (error) {
    executionError = error;
} finally {
    globalThis.fetch = nativeFetch;
    await new Promise((resolveClose, rejectClose) => {
        server.close((error) => (error ? rejectClose(error) : resolveClose()));
    });
}

assert.deepEqual(
    forbiddenRequests,
    [],
    `The success hook crossed a forbidden GitHub boundary: ${forbiddenRequests.join(", ")}`,
);
if (executionError) {
    throw executionError;
}

assert.equal(
    requests.filter((request) => request.method === "POST").length,
    histories.length,
);
assert.ok(
    requests.every(
        (request) =>
            (request.method === "GET" && request.pathname === repositoryPath) ||
            (request.method === "POST" &&
                request.pathname.endsWith("/graphql")),
    ),
);
console.log(
    `semantic-release GitHub success fixture passed ${histories.length} histories with @semantic-release/github ${installedPackage.version} through ${requests.length} loopback requests`,
);
