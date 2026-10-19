text(await tools.exec_command({cmd:"rg -n -A 84 -B 4 'def attach_isolation' tools/tests/test_p1r_published_executor.py",workdir:load("p1r_31251_run_state").integration_root,max_output_tokens:6000}));
text(await tools.exec_command({cmd:"rg -n -A 65 -B 6 'def import_fixture' tools/tests/test_p1r_published_executor.py",workdir:load("p1r_31251_run_state").integration_root,max_output_tokens:4500}));
