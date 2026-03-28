[← Back to Hexalith.EventStore](../../README.md)

# Regenerate Quickstart Demo GIF

Step-by-step procedure to reproduce `docs/assets/quickstart-demo.gif`. Follow this checklist when the demo needs updating after UI or workflow changes.

## Prerequisites

Before recording, ensure you have:

- [.NET 10 SDK](https://dotnet.microsoft.com/download) installed
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) running
- [DAPR CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) initialized (`dapr init`)
- A screen recording tool (see [Recording Tools](#recording-tools) below)
- [ffmpeg](https://ffmpeg.org/) and [gifsicle](https://www.lcdf.org/gifsicle/) installed for conversion and optimization

## Start the Application

1. Open a terminal at the repository root.
2. Run the Aspire AppHost:

   ```bash
   $ dotnet run --project src/Hexalith.EventStore.AppHost
   ```

3. Wait for the Aspire dashboard to open automatically (typically `https://localhost:15888` or as displayed in the terminal output).
4. Verify all services are healthy in the Aspire dashboard: eventstore, sample, redis, and keycloak (if enabled).
5. Open Swagger UI at the CommandAPI port (typically `https://localhost:8080/swagger` — check `launchSettings.json` for the exact port).

## Capture the Recording

Set your capture area to approximately 960px wide. Record the following sequence in order:

1. **Aspire dashboard overview (2-3 seconds)** — show all services with green/healthy indicators.
2. **Switch to Swagger UI (3-4 seconds)** — navigate to a command endpoint (e.g., POST IncrementCounter), fill in a sample payload, and execute the command.
3. **API response (1-2 seconds)** — show the 200/202 success response with the correlation ID.
4. **Aspire traces/logs (3-4 seconds)** — switch back to the Aspire dashboard and show the trace spanning eventstore, actor, sample, state store, and pub/sub.

Target total duration: 10-15 seconds.

> **Note:** The goal is to show the complete command-to-event flow. Make sure text is readable at GitHub's default image rendering width.

## Convert and Optimize

### Convert to GIF

Use ffmpeg to convert the screen recording to GIF with an optimized palette:

```bash
$ ffmpeg -i recording.mp4 -vf "fps=10,scale=960:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse" -loop 0 raw.gif
```

### Optimize with Gifsicle

Use gifsicle to reduce file size:

```bash
$ gifsicle -O3 --lossy=80 raw.gif -o docs/assets/quickstart-demo.gif
```

## Verify File Size

The GIF must be under 5MB (5,242,880 bytes):

```bash
$ ls -la docs/assets/quickstart-demo.gif
```

If the file exceeds 5MB, apply one or more of these optimization levers:

- Reduce frame rate: change `fps=10` to `fps=8` or `fps=6`
- Reduce resolution: change `scale=960:-1` to `scale=800:-1` or `scale=640:-1`
- Increase lossy compression: change `--lossy=80` to `--lossy=100` or `--lossy=120`
- Trim unnecessary frames to reduce capture duration
- Crop to the relevant area only (remove browser chrome if not needed)

## Recording Tools

Any screen recording tool that exports to MP4 or MOV works. Recommended options:

| Tool | Platform | Notes |
|------|----------|-------|
| [ScreenToGif](https://www.screentogif.com/) | Windows | Captures and optimizes in one tool, no ffmpeg/gifsicle needed |
| [LICEcap](https://www.cockos.com/licecap/) | Windows, macOS | Direct GIF capture with built-in optimization |
| [OBS Studio](https://obsproject.com/) | Windows, macOS, Linux | Full-featured recorder, exports MP4 for ffmpeg conversion |
| [Gifski](https://gif.ski/) | Windows, macOS | High-quality GIF encoder from PNG frames |

> **Note:** If you use ScreenToGif or LICEcap, you can skip the ffmpeg conversion step and go directly to the gifsicle optimization step (or skip that too if the tool's built-in optimization keeps the file under 5MB).

## Next Steps

- **Next:** [README](../../README.md) — verify the GIF renders correctly
- **Related:** [Prerequisites](../getting-started/prerequisites.md), [Quickstart Guide](../getting-started/quickstart.md)
