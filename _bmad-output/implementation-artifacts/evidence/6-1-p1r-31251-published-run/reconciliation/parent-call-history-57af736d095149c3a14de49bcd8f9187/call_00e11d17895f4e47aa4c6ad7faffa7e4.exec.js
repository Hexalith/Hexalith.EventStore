text(await tools.exec_command({cmd:"rg -n 'check_current_source\\(' tools | head -70",workdir:load("p1r_31251_run_state").integration_root,max_output_tokens:3500}));
text(await tools.write_stdin({session_id:87220,chars:"",yield_time_ms:1000,max_output_tokens:3500}));
