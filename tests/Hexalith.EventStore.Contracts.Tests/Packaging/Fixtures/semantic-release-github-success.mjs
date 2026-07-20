import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createServer } from "node:http";
import { createRequire } from "node:module";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { isDeepStrictEqual } from "node:util";
import {
    Dispatcher,
    fetch as undiciFetch,
    getGlobalDispatcher,
    setGlobalDispatcher,
} from "undici";

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
assert.deepEqual(Object.keys(githubOptions).sort(), [
    "assets",
    "successCommentCondition",
]);
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
const fixtureUndiciEntry = require.resolve("undici");
const pluginUndiciEntry = require.resolve("undici", {
    paths: [dirname(installedEntry)],
});
assert.equal(
    pluginUndiciEntry,
    fixtureUndiciEntry,
    "The fixture must guard the same Undici module resolved by the installed GitHub plugin.",
);
const installedPackage = JSON.parse(
    await readFile(resolve(dirname(installedEntry), "package.json"), "utf8"),
);
assert.equal(installedPackage.name, "@semantic-release/github");
assert.equal(
    installedPackage.version,
    lockedPackage.version,
    "The installed GitHub plugin must match package-lock.json.",
);

const owner = "Hexalith";
const repo = "Hexalith.EventStore";
const repository = `${owner}/${repo}`;
const repositoryPath = `/repos/${repository}`;
const repositoryUrl = `https://github.com/${repository}.git`;
const runIds = ["29738838856", "29720431798"];
const expectedGetSRIssuesQuery = `#graphql
  query getSRIssues($owner: String!, $repo: String!, $filter: IssueFilters) {
    repository(owner: $owner, name: $repo) {
      issues(first: 100, states: OPEN, filterBy: $filter) {
        nodes {
          number
          title
          body
        }
      }
    }
  }
`;
const expectedGetSRIssuesRequest = {
    query: expectedGetSRIssuesQuery,
    variables: {
        filter: { labels: ["semantic-release", "semantic-release"] },
        owner,
        repo,
    },
};
const requests = [];
const forbiddenRequests = [];
let selectedOrigin;

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

const rejectRequest = (response, record, reason) => {
    forbiddenRequests.push(`${record.method} ${record.rawUrl}: ${reason}`);
    respondJson(response, 418, {
        message: `Forbidden fixture request: ${reason}`,
    });
};

const handleRequest = async (request, response) => {
    const method = request.method ?? "UNKNOWN";
    const rawUrl = request.url ?? "/";
    const requestUrl = new URL(rawUrl, selectedOrigin);
    const body = await readRequestBody(request);
    const record = {
        body,
        method,
        pathname: requestUrl.pathname,
        rawUrl,
        search: requestUrl.search,
    };
    requests.push(record);

    const remoteAddress = request.socket.remoteAddress ?? "";
    const isLoopbackPeer = ["127.0.0.1", "::1", "::ffff:127.0.0.1"].includes(
        remoteAddress,
    );
    if (!isLoopbackPeer) {
        rejectRequest(response, record, `non-loopback peer ${remoteAddress}`);
        return;
    }

    if (
        requestUrl.origin !== selectedOrigin ||
        request.headers.host !== new URL(selectedOrigin).host
    ) {
        rejectRequest(
            response,
            record,
            "request did not use the selected origin",
        );
        return;
    }

    const serializedRequest = `${rawUrl}\n${body}`;
    if (runIds.some((runId) => serializedRequest.includes(runId))) {
        rejectRequest(response, record, "URL or body contains a CI run ID");
        return;
    }

    if (requestUrl.search.length > 0) {
        rejectRequest(response, record, "query strings are not allowed");
        return;
    }

    if (method === "GET" && rawUrl === repositoryPath && body.length === 0) {
        respondJson(response, 200, {
            clone_url: repositoryUrl,
            default_branch: "main",
            full_name: repository,
            permissions: { push: true },
        });
        return;
    }

    if (method === "POST" && rawUrl === "/graphql") {
        let graphql;
        try {
            graphql = JSON.parse(body);
        } catch {
            rejectRequest(response, record, "GraphQL body is not valid JSON");
            return;
        }

        if (!isDeepStrictEqual(graphql, expectedGetSRIssuesRequest)) {
            rejectRequest(
                response,
                record,
                `GraphQL document or variables differ: ${JSON.stringify(graphql)}`,
            );
            return;
        }

        respondJson(response, 200, {
            data: { repository: { issues: { nodes: [] } } },
        });
        return;
    }

    const isNumericNotificationMutation =
        ["DELETE", "PATCH", "POST", "PUT"].includes(method) &&
        /\/(?:issues|pulls)\/\d+(?:\/(?:comments|labels))?$/.test(
            requestUrl.pathname,
        );
    const reason = requestUrl.pathname.includes("graphql", 1)
        ? "unexpected GraphQL endpoint"
        : isNumericNotificationMutation
          ? "numeric comment or label mutation"
          : "unexpected endpoint";
    rejectRequest(response, record, reason);
};

const server = createServer((request, response) => {
    void handleRequest(request, response).catch((error) => {
        const record = `${request.method ?? "UNKNOWN"} ${request.url ?? "/"}`;
        forbiddenRequests.push(`${record}: handler error ${error.message}`);
        if (!response.headersSent) {
            respondJson(response, 500, { message: "Fixture handler failed" });
        } else {
            response.destroy(error);
        }
    });
});

await new Promise((resolveListen, rejectListen) => {
    server.once("error", rejectListen);
    server.listen(0, "127.0.0.1", resolveListen);
});

const address = server.address();
assert.ok(address && typeof address === "object");
selectedOrigin = `http://127.0.0.1:${address.port}`;
assert.equal(new URL(selectedOrigin).origin, selectedOrigin);

class SelectedOriginDispatcher extends Dispatcher {
    constructor(delegate, allowedOrigin, delegatedRequests, blockedRequests) {
        super();
        this.delegate = delegate;
        this.allowedOrigin = allowedOrigin;
        this.delegatedRequests = delegatedRequests;
        this.blockedRequests = blockedRequests;
    }

    dispatch(options, handler) {
        const origin = new URL(String(options.origin)).origin;
        const target = new URL(options.path, `${origin}/`);
        if (origin !== this.allowedOrigin) {
            this.blockedRequests.push(target.href);
            throw new Error(`Undici egress blocked before I/O: ${target.href}`);
        }

        this.delegatedRequests.push(target.href);
        return this.delegate.dispatch(options, handler);
    }

    close(callback) {
        if (callback) {
            queueMicrotask(callback);
            return undefined;
        }

        return Promise.resolve();
    }

    destroy(errorOrCallback, callback) {
        const completion =
            typeof errorOrCallback === "function" ? errorOrCallback : callback;
        if (completion) {
            queueMicrotask(completion);
            return undefined;
        }

        return Promise.resolve();
    }
}

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
const delegatedRequests = [];
const blockedRequests = [];
const publicSuccessWrappers = [];
const originalDispatcher = getGlobalDispatcher();
const guardedDispatcher = new SelectedOriginDispatcher(
    originalDispatcher,
    selectedOrigin,
    delegatedRequests,
    blockedRequests,
);
const externalProbe = "https://api.github.com/semantic-release-egress-probe";
let executionError;

setGlobalDispatcher(guardedDispatcher);
try {
    await assert.rejects(
        undiciFetch(externalProbe),
        "The actual Undici dispatcher guard must block an external probe.",
    );
    assert.deepEqual(blockedRequests, [externalProbe]);

    for (const [historyIndex, history] of histories.entries()) {
        const pluginWrapperUrl = pathToFileURL(installedEntry);
        pluginWrapperUrl.searchParams.set(
            "fixture-history",
            String(historyIndex),
        );
        const { success: githubSuccess } = await import(pluginWrapperUrl.href);
        publicSuccessWrappers.push(githubSuccess);

        const requestCountBeforeHistory = requests.length;
        await githubSuccess(
            {
                ...githubOptions,
                githubApiUrl: selectedOrigin,
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
        assert.equal(
            historyRequests.length,
            3,
            `${history.name} must independently verify the public wrapper, read repository metadata, and clean stale failures.`,
        );
        assert.equal(
            historyRequests.filter(
                (request) =>
                    request.method === "GET" &&
                    request.rawUrl === repositoryPath,
            ).length,
            2,
            `${history.name} must use a fresh public plugin wrapper with independent verified state.`,
        );
        assert.equal(
            historyRequests.filter(
                (request) =>
                    request.method === "POST" &&
                    request.rawUrl === "/graphql" &&
                    isDeepStrictEqual(
                        JSON.parse(request.body),
                        expectedGetSRIssuesRequest,
                    ),
            ).length,
            1,
            `${history.name} must perform exactly one pinned stale-failure cleanup query.`,
        );
    }
} catch (error) {
    executionError = error;
} finally {
    setGlobalDispatcher(originalDispatcher);
    await new Promise((resolveClose, rejectClose) => {
        server.close((error) => (error ? rejectClose(error) : resolveClose()));
    });
}

assert.deepEqual(
    forbiddenRequests,
    [],
    `The success hook crossed a forbidden GitHub boundary: ${forbiddenRequests.join(", ")}`,
);
assert.deepEqual(
    blockedRequests,
    [externalProbe],
    "Only the intentional external dispatcher probe may reach the egress guard.",
);
if (executionError) {
    throw executionError;
}

assert.equal(new Set(publicSuccessWrappers).size, histories.length);
assert.equal(requests.length, histories.length * 3);
assert.equal(delegatedRequests.length, requests.length);
assert.ok(
    delegatedRequests.every(
        (requestUrl) => new URL(requestUrl).origin === selectedOrigin,
    ),
);
assert.ok(
    requests.every(
        (request) =>
            (request.method === "GET" && request.rawUrl === repositoryPath) ||
            (request.method === "POST" && request.rawUrl === "/graphql"),
    ),
);
console.log(
    `semantic-release GitHub success fixture passed ${histories.length} isolated histories with @semantic-release/github ${installedPackage.version} through ${requests.length} selected-origin requests and blocked 1 external Undici probe`,
);
