export function getUtcOffsetMinutesForLocalDateTime(year, month, day, hour, minute) {
  const localDateTime = new Date(year, month - 1, day, hour, minute, 0, 0);
  return -localDateTime.getTimezoneOffset();
}

export function getLocalDateTimePartsFromUtc(utcIsoString) {
  const localDateTime = new Date(utcIsoString);

  return {
    year: localDateTime.getFullYear(),
    month: localDateTime.getMonth() + 1,
    day: localDateTime.getDate(),
    hour: localDateTime.getHours(),
    minute: localDateTime.getMinutes(),
    second: localDateTime.getSeconds()
  };
}