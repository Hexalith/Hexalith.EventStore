import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createServer } from "node:http";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { success as githubSuccess } from "@semantic-release/github";

const fixtureDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(fixtureDirectory, "../../../..");
const releaseConfiguration = JSON.parse(
  await readFile(resolve(repositoryRoot, ".releaserc.json"), "utf8"),
);
const githubPlugin = releaseConfiguration.plugins.find(
  (plugin) => Array.isArray(plugin) && plugin[0] === "@semantic-release/github",
);

assert.ok(githubPlugin, "The GitHub semantic-release plugin must be configured.");
const githubOptions = githubPlugin[1];
assert.deepEqual(githubOptions.assets, ["nupkgs/*.nupkg"]);
assert.equal(githubOptions.successCommentCondition, false);
assert.equal(Object.hasOwn(githubOptions, "successComment"), false);

const repository = "Hexalith/Hexalith.EventStore";
const repositoryUrl = `https://github.com/${repository}.git`;
const requests = [];
const forbiddenRequests = [];
const server = createServer((request, response) => {
  const method = request.method ?? "UNKNOWN";
  const pathname = new URL(request.url ?? "/", "http://fixture.invalid").pathname;
  const record = `${method} ${pathname}`;
  requests.push(record);
  request.resume();

  const isGraphQl = pathname.endsWith("/graphql");
  const isNumericNotificationMutation =
    ["DELETE", "PATCH", "POST", "PUT"].includes(method) &&
    /\/(?:issues|pulls)\/\d+(?:\/(?:comments|labels))?$/.test(
      pathname,
    );
  if (isGraphQl || isNumericNotificationMutation) {
    forbiddenRequests.push(record);
    response.writeHead(418, { "content-type": "application/json" });
    response.end(JSON.stringify({ message: `Forbidden fixture request: ${record}` }));
    return;
  }

  if (method === "GET" && pathname === "/repos/Hexalith/Hexalith.EventStore") {
    response.writeHead(200, { "content-type": "application/json" });
    response.end(
      JSON.stringify({
        clone_url: repositoryUrl,
        default_branch: "main",
        full_name: repository,
        permissions: { push: true },
      }),
    );
    return;
  }

  forbiddenRequests.push(record);
  response.writeHead(418, { "content-type": "application/json" });
  response.end(JSON.stringify({ message: `Unexpected fixture request: ${record}` }));
});

await new Promise((resolveListen, rejectListen) => {
  server.once("error", rejectListen);
  server.listen(0, "127.0.0.1", resolveListen);
});

const address = server.address();
assert.ok(address && typeof address === "object");
const githubApiUrl = `http://127.0.0.1:${address.port}`;
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
let executionError;

try {
  for (const history of histories) {
    const requestCountBeforeHistory = requests.length;
    await githubSuccess(
      {
        ...githubOptions,
        // Stale semantic-release failure cleanup is outside this success-notification fixture.
        failCommentCondition: false,
        githubApiUrl,
        proxy: false,
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
    assert.ok(
      requests.length > requestCountBeforeHistory,
      `${history.name} must exercise the fake GitHub HTTP boundary.`,
    );
  }
} catch (error) {
  executionError = error;
} finally {
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

assert.ok(requests.length >= histories.length);
assert.ok(
  requests.every(
    (request) => request === "GET /repos/Hexalith/Hexalith.EventStore",
  ),
);
console.log(
  `semantic-release GitHub success fixture passed ${histories.length} histories through ${requests.length} fake requests`,
);
