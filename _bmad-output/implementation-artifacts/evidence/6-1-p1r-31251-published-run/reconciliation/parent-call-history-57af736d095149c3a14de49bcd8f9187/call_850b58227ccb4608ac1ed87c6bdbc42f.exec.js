const s=load("p1r_31251_run_state");text(await tools.exec_command({cmd:"rg -n -A 40 -B 5 '^def check_current_source' tools/p1r_qualification.py",workdir:s.integration_root,max_output_tokens:2500}));
text(await tools.exec_command({cmd:"rg -n -A 35 -B 5 '^def check_current_source' tools/p1r_qualification.py",workdir:s.verifier_v4,max_output_tokens:2500}));
text(await tools.clock__curr_time({}));
