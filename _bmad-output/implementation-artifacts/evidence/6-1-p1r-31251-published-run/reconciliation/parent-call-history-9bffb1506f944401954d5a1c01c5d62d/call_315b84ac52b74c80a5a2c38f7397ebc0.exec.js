const out=await Promise.allSettled([
tools.exec_command({cmd:"git status --short --branch",max_output_tokens:2000}),
tools.exec_command({cmd:"git log -3 --oneline",max_output_tokens:1500}),
tools.exec_command({cmd:"git remote -v",max_output_tokens:1000}),
tools.exec_command({cmd:"rg --files --hidden --no-ignore _bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-published-run/diagnostic-7d9c2147a13a4903b5b6193859d7cbb4",workdir:"/tmp/p1r-31251-verifier-import-c237110a4d04422f85ed2794536848d4",max_output_tokens:2200})
]);for(let i=0;i<out.length;i++)text({i,...out[i]});
