import { resolveLabels } from './lookup.util';

interface Option {
  pkid: number;
  title: string;
}

describe('resolveLabels', () => {
  const options: Option[] = [
    { pkid: 5, title: 'MCSA' },
    { pkid: 7, title: 'MCSE' }
  ];

  it('maps pkids to their labels, in the order given', () => {
    expect(resolveLabels([7, 5], options, o => o.pkid, o => o.title)).toEqual(['MCSE', 'MCSA']);
  });

  it('keeps an id the lookup is missing as #id rather than dropping it', () => {
    expect(resolveLabels([5, 99], options, o => o.pkid, o => o.title)).toEqual(['MCSA', '#99']);
  });

  it('degrades every label to #id when the lookup came back empty', () => {
    expect(resolveLabels([5, 7], [], (o: Option) => o.pkid, (o: Option) => o.title)).toEqual(['#5', '#7']);
  });

  it('returns nothing for a record with no N-N rows', () => {
    expect(resolveLabels([], options, o => o.pkid, o => o.title)).toEqual([]);
  });
});
