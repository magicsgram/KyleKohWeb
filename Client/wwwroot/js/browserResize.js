window.browserResize = {
  getInnerHeight: function () {
    return window.innerHeight;
  },
  getInnerWidth: function () {
    return window.innerWidth;
  },
  registerResizeCallback: function () {
    window.addEventListener("resize", browserResize.resized);
  },
  resized: function () {
    DotNet.invokeMethodAsync("KyleKoh.Client", 'OnBrowserResize').then(data => data);
  }
}