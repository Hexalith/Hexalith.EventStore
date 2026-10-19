text(await tools.exec_command({cmd:"sed -n '279,554p' /tmp/p1r-31251-additional-tracked-review-e7a97404476f459282e0a9949f67453c.patch",max_output_tokens:7500}));
text(await tools.exec_command({cmd:"git diff --no-ext-diff --no-textconv --submodule=short d55c6f7c11e9bce6f17d490a84c83f1561979d63 -- references",workdir:load("p1r_31251_run_state").integration_root,max_output_tokens:2500}));
