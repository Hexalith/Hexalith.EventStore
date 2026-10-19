const rs=await Promise.allSettled([
tools.exec_command({cmd:"python3 - <<'PY'\nimport hashlib,re\nfrom pathlib import Path\np=Path('_bmad-output/implementation-artifacts/spec-6-1-p1r-31251-published-qualification.md')\nb=p.read_bytes();block=re.search(rb'<frozen-after-approval\\b.*?</frozen-after-approval>',b,re.S).group()\nprint({'whole_file_sha256':hashlib.sha256(b).hexdigest(),'frozen_block_sha256':hashlib.sha256(block).hexdigest()})\nPY",workdir:"/home/administrator/projects/hexalith/eventstore",max_output_tokens:1200}),
tools.write_stdin({session_id:70847,chars:"",yield_time_ms:1000,max_output_tokens:1600}),
tools.clock__curr_time({})
]);for(let i=0;i<rs.length;i++)text({i,...rs[i]});
