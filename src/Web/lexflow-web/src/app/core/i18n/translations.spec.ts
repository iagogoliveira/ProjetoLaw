import en from './en.json';
import ptBR from './pt-BR.json';

function keys(value: object, prefix = ''): string[] {
  return Object.entries(value).flatMap(([key, child]) =>
    typeof child === 'object' && child !== null
      ? keys(child as object, `${prefix}${key}.`)
      : [`${prefix}${key}`],
  );
}

describe('translations', () => {
  it('en has exactly the same keys as pt-BR', () => {
    expect(keys(en).sort()).toEqual(keys(ptBR).sort());
  });
});
