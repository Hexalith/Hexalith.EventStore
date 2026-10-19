const o=await Promise.allSettled([
tools.exec_command({cmd:"du -sh _bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-published-run",workdir:"/tmp/p1r-31251-verifier-import-c237110a4d04422f85ed2794536848d4",max_output_tokens:1000}),
tools.exec_command({cmd:"du -sh _bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-published-run",workdir:"/tmp/p1r-31251-verifier-final-78617511ec9c4208b1267e1cde6e21bf",max_output_tokens:1000}),
tools.exec_command({cmd:"du -sh _bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-published-run",workdir:"/tmp/p1r-31251-verifier-38d60cf41afc4f2d99b4b377c016f95c",max_output_tokens:1000})
]);for(let i=0;i<o.length;i++)text({i,...o[i]});
