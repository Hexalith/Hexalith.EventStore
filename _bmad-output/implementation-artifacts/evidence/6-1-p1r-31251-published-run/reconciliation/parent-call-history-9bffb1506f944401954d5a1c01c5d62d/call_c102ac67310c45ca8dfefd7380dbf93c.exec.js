text(await tools.exec_command({cmd:"rg -n -A 74 '^def separate_source_fixture' tools/tests/p1r_isolation_fixture.py",workdir:"/tmp/p1r-31251-integration-86e5a7710e44434f915076eb2751ab31",max_output_tokens:5000}));
text(await tools.exec_command({cmd:"rg -n -C 3 'build_consumers|global.json' tools/tests/test_p1r_published_executor.py",workdir:"/tmp/p1r-31251-integration-86e5a7710e44434f915076eb2751ab31",max_output_tokens:3000}));
text(await tools.clock__curr_time({}));
