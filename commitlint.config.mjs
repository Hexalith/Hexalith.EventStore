import createConventionalPreset from 'conventional-changelog-conventionalcommits';

const { parser } = createConventionalPreset();

export default {
  extends: ['@commitlint/config-conventional'],
  // Both the CLI and the shared PR title check use these parser options.
  parserPreset: {
    parserOpts: {
      ...parser,
      headerPattern: /^(\w*)(?:\((.*)\))?!?: (\p{Ll}.*)$/u,
      breakingHeaderPattern: /^(\w*)(?:\((.*)\))?!: (\p{Ll}.*)$/u,
    },
  },
  rules: {
    'type-enum': [
      2,
      'always',
      ['build', 'ci', 'docs', 'feat', 'fix', 'perf', 'refactor', 'revert', 'style', 'test'],
    ],
    'header-max-length': [2, 'always', 200],
    'body-max-line-length': [2, 'always', 200],
    'footer-max-line-length': [2, 'always', 200],
  },
};
