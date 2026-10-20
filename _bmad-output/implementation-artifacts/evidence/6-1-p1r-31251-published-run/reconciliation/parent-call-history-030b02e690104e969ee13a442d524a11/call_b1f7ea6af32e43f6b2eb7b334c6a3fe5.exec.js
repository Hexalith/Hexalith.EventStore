const rs=await Promise.allSettled([
tools.write_stdin({session_id:72175,chars:"",yield_time_ms:1000,max_output_tokens:2500}),
tools.exec_command({cmd:"git diff --check",workdir:"/home/administrator/projects/hexalith/eventstore",yield_time_ms:1000,max_output_tokens:1200}),
tools.clock__curr_time({})
]);for(let i=0;i<rs.length;i++)text({i,...rs[i]});
