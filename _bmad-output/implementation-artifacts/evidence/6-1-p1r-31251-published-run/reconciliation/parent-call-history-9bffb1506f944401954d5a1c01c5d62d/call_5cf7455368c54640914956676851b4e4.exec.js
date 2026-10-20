text(await tools.exec_command({cmd:"sed -n '173,258p' tools/p1r_isolation.py",workdir:"/tmp/p1r-31251-integration-86e5a7710e44434f915076eb2751ab31",max_output_tokens:5000}));
text(await tools.exec_command({cmd:"sed -n '152,208p' tools/p1r_isolation.py",workdir:"/tmp/p1r-31251-verifier-import-c237110a4d04422f85ed2794536848d4",max_output_tokens:3500}));
text(await tools.exec_command({cmd:"rg -n -C 5 'controller_source_binding|source_root|sdk_arguments' tools/p1r_isolation.py",workdir:"/tmp/p1r-31251-integration-86e5a7710e44434f915076eb2751ab31",max_output_tokens:6500}));
