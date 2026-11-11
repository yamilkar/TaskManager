import { initials } from './initials';

describe('initials', () => {
  it('initials_twoNames_takesFirstLetters', () => {
    expect(initials('Ana Lopez')).toBe('AL');
  });

  it('initials_manyNames_takesFirstAndLast', () => {
    expect(initials('Ana Maria de la Cruz')).toBe('AC');
  });

  it('initials_oneName_takesItsFirstLetter', () => {
    expect(initials('  ana ')).toBe('A');
  });

  it('initials_empty_isEmpty', () => {
    expect(initials('')).toBe('');
  });
});
