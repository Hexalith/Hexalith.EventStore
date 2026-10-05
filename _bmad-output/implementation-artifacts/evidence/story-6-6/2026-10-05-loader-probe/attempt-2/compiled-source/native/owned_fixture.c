#include <stdio.h>
#include <stdlib.h>

/* Every fixture effect targets only the private temporary path supplied by the runner. */
static int append_marker(const char *path, const char *marker)
{
    FILE *file = fopen(path, "a");
    if (file == NULL)
    {
        return 1;
    }

    int failed = fputs(marker, file) < 0;
    return fclose(file) != 0 || failed;
}

/* The ELF constructor demonstrates code execution before NativeLibrary.Load returns. */
__attribute__((constructor)) static void on_load(void)
{
    const char *path = getenv("STORY_6_6_OWNED_EFFECTS");
    if (path != NULL)
    {
        (void)append_marker(path, "native:constructor\n");
    }
}

/* The callable export separately proves an unmanifested native function executed. */
int probe_write(const char *path)
{
    return append_marker(path, "native:export\n");
}
