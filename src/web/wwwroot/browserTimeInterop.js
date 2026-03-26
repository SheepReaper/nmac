export function getUtcOffsetMinutesForLocalDateTime(year, month, day, hour, minute) {
  const localDateTime = new Date(year, month - 1, day, hour, minute, 0, 0);
  return -localDateTime.getTimezoneOffset();
}

let deferredImageObserver;
const deferredImageQueue = [];
const queuedImages = new WeakSet();
let activeDeferredImageLoads = 0;
let maxDeferredImageLoads = 4;

function getDeferredImageObserver() {
  if (deferredImageObserver) {
    return deferredImageObserver;
  }

  deferredImageObserver = new IntersectionObserver((entries) => {
    for (const entry of entries) {
      if (!entry.isIntersecting) {
        continue;
      }

      const image = entry.target;
      deferredImageObserver.unobserve(image);
      queueDeferredImage(image);
    }
  }, {
    rootMargin: "250px 0px"
  });

  return deferredImageObserver;
}

function queueDeferredImage(image) {
  if (queuedImages.has(image)) {
    return;
  }

  queuedImages.add(image);
  deferredImageQueue.push(image);
  pumpDeferredImageQueue();
}

function pumpDeferredImageQueue() {
  while (activeDeferredImageLoads < maxDeferredImageLoads && deferredImageQueue.length > 0) {
    const image = deferredImageQueue.shift();
    if (!image || image.dataset.avatarLoaded === "1") {
      continue;
    }

    const deferredSource = image.getAttribute("data-avatar-src");
    if (!deferredSource) {
      continue;
    }

    activeDeferredImageLoads += 1;

    const preloader = new Image();
    preloader.decoding = "async";
    preloader.referrerPolicy = "no-referrer";

    const finalize = () => {
      activeDeferredImageLoads -= 1;
      pumpDeferredImageQueue();
    };

    preloader.onload = () => {
      if (image.isConnected) {
        image.src = deferredSource;
        image.dataset.avatarLoaded = "1";
      }

      finalize();
    };

    preloader.onerror = () => {
      if (image.isConnected) {
        image.remove();
      }

      finalize();
    };

    preloader.src = deferredSource;
  }
}

export function hydrateDeferredImages(selector, maxConcurrentLoads = 4) {
  if (typeof selector !== "string" || selector.length === 0) {
    return;
  }

  maxDeferredImageLoads = Number.isFinite(maxConcurrentLoads) && maxConcurrentLoads > 0
    ? Math.floor(maxConcurrentLoads)
    : 4;

  const container = document.querySelector(selector);
  if (!container) {
    return;
  }

  const observer = getDeferredImageObserver();
  const images = container.querySelectorAll("img[data-avatar-src]:not([data-avatar-loaded='1'])");

  for (const image of images) {
    observer.observe(image);
  }
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