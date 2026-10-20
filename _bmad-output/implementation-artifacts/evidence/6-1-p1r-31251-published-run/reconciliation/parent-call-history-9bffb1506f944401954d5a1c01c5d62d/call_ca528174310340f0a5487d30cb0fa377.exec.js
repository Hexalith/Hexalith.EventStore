const results=await Promise.allSettled([
tools.exec_command({cmd:"git show --format=fuller --stat 2817c2cc",max_output_tokens:3000}),
tools.exec_command({cmd:"git diff bb315b1f3379e816cd63f270264518228551746a HEAD -- tools/p1r_published_executor.py tools/p1r_qualification.py tools/p1r-published-consumers/README.md",max_output_tokens:11000}),
tools.exec_command({cmd:"cat _bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-published-run/diagnostic-7d9c2147a13a4903b5b6193859d7cbb4/diagnostic.json",workdir:"/tmp/p1r-31251-verifier-import-c237110a4d04422f85ed2794536848d4",max_output_tokens:4500})
]);for(let i=0;i<results.length;i++)text({i,...results[i]});
