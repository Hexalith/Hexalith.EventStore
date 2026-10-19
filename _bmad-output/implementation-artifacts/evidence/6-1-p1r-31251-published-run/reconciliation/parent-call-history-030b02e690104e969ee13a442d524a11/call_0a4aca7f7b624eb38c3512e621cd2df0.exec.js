const s=load("p1r_31251_run_state");
const rs=await Promise.allSettled([
tools.exec_command({cmd:"rg -n '\\.log|coverage|bmad|evidence|p1r' .gitignore .git/info/exclude",workdir:s.integration_root,max_output_tokens:1800}),
tools.exec_command({cmd:"git check-ignore -v _bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-published-run/reconciliation/verification-ce6d5948928243f8a44978c7d1fef11a/qualification.log",workdir:s.integration_root,max_output_tokens:1600}),
tools.clock__curr_time({})
]);for(let i=0;i<rs.length;i++)text({i,...rs[i]});
