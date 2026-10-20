text(await tools.exec_command({cmd:"ls /tmp/p1r-31251-source-review-*",max_output_tokens:1800}));
text(await tools.exec_command({cmd:"git status --short --branch; git remote -v; git log -5 --oneline",workdir:"/home/administrator/projects/hexalith/eventstore",max_output_tokens:2500}));
text(await tools.write_stdin({session_id:87220,chars:"",yield_time_ms:1000,max_output_tokens:2400}));
