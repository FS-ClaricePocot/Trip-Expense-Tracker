import { tripPeopleText } from './trip-count-text';

describe('tripPeopleText (F3.R13)', () => {
  it('says "No people yet" for no people (F3.AC16)', () => {
    expect(tripPeopleText(0)).toBe('No people yet');
  });

  it('says "1 person" for one, not "1 people" (F3.R13)', () => {
    expect(tripPeopleText(1)).toBe('1 person');
  });

  it('says "{n} people" for more than one (F3.AC15)', () => {
    expect(tripPeopleText(3)).toBe('3 people');
  });
});
