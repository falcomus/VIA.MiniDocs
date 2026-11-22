export default {
  rules: {
    'at-rule-no-unknown': true,
    'block-no-empty': true,
    'color-no-invalid-hex': true,
    'comment-no-empty': true,
    'declaration-block-no-duplicate-properties': true,
    'declaration-block-no-shorthand-property-overrides': true,
    'declaration-no-important': true,
    'declaration-property-value-no-unknown': true,
    'font-family-no-duplicate-names': true,
    'function-calc-no-unspaced-operator': true,
    'keyframe-declaration-no-important': true,
    'no-duplicate-at-import-rules': true,
    'no-duplicate-selectors': true,
    'no-invalid-double-slash-comments': true,
    'property-no-unknown': true,
    'selector-max-specificity': '0,4,2',
    'selector-pseudo-class-no-unknown': true,
    'selector-pseudo-element-no-unknown': true,
    'unit-no-unknown': true,
  },
  overrides: [
    // CSS Modules use these constructs in addition to standard CSS.
    {
      files: ['**/*.module.css'],
      rules: {
        'at-rule-no-unknown': [true, { ignoreAtRules: ['value'] }],
        'property-no-unknown': [true, { ignoreProperties: ['composes'] }],
        'selector-pseudo-class-no-unknown': [true, { ignorePseudoClasses: ['global', 'local'] }],
      },
    },
    // #root is the Vite mount point, so the global reset needs one ID selector.
    { files: ['src/styles/global.css'], rules: { 'selector-max-specificity': '1,0,0' } },
  ],
};
