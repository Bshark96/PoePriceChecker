// Unified PoE 1 Banner Renderer & Construction Engine
function BannerRenderer(bannerId) {
  this.bannerEl = typeof bannerId === 'string' ? document.getElementById(bannerId) : bannerId;
}

BannerRenderer.prototype.applyTheme = function(themeOrKey) {
  var theme = (typeof themeOrKey === 'string') 
    ? (dataPresets[themeOrKey] || dataPresets.theme4) 
    : themeOrKey;

  BannerRenderer.applyThemeToBanner(this.bannerEl, theme);
};

// Unified Banner DOM Construction Function
BannerRenderer.constructBannerDOM = function(containerEl, title, subtitle) {
  if (!containerEl) return;

  containerEl.className = 'item-banner';
  containerEl.innerHTML = 
    '<img src="VisualAssets/08_left_endcap_trimmed.png" class="banner-endcap left" id="endcapLeft" alt="" />' +
    '<img src="VisualAssets/08_right_endcap_trimmed.png" class="banner-endcap right" id="endcapRight" alt="" />' +
    '<img src="VisualAssets/02_right_ornament.png" class="banner-ornament left" id="ornamentLeft" alt="" />' +
    '<img src="VisualAssets/01_left_ornament.png" class="banner-ornament right" id="ornamentRight" alt="" />' +
    '<div class="item-name" id="itemName">' + (title || 'WAITING FOR ITEM') + '</div>' +
    '<div class="item-base" id="itemBase">' + (subtitle || 'PRESS CTRL+D IN PATH OF EXILE 1') + '</div>' +
    '<img src="VisualAssets/04_straight_backing_bar.png" class="banner-bar-bottom" id="backingBar" alt="" />' +
    '<div class="banner-center-diamond" id="centerDiamond">' +
      '<img src="VisualAssets/03_center_diamond_with_mount.png" class="diamond-mount" id="diamondMount" alt="" />' +
      '<div class="diamond-core" id="diamondCore">' +
        '<img src="VisualAssets/03_center_diamond_with_mount.png" class="gem-texture-img" id="gemTextureImg" alt="" />' +
      '</div>' +
    '</div>';
};

// Unified Theme Application Function (applies to Evaluate Banner, Alarm Banner, or any card)
BannerRenderer.applyThemeToBanner = function(bannerEl, theme) {
  if (!bannerEl || !theme) return;

  // 1. Root Banner Styling
  bannerEl.style.background = theme.bannerBg || 'linear-gradient(180deg, #1b1704 0%, #292407 45%, #1c1804 75%, #0e0c01 100%)';
  bannerEl.style.borderColor = theme.bannerBorder || '#a89022';

  // 2. Ornaments
  var filterVal = theme.endcapFilter || 'none';
  var ornFilter = 'contrast(0) brightness(1.2) ' + filterVal;
  var ornaments = bannerEl.querySelectorAll('.banner-ornament');
  for (var i = 0; i < ornaments.length; i++) {
    ornaments[i].style.filter = ornFilter;
  }

  // 3. Endcaps Outline and Saturation (prevent downward protrusion past bottom border)
  var shapeOutline = 'drop-shadow(1px 0 0 ' + theme.bannerBorder + ') drop-shadow(-1px 0 0 ' + theme.bannerBorder + ') drop-shadow(0 -1px 0 ' + theme.bannerBorder + ')';
  var endcaps = bannerEl.querySelectorAll('.banner-endcap');
  for (var j = 0; j < endcaps.length; j++) {
    endcaps[j].style.filter = filterVal + ' ' + shapeOutline;
  }

  // 4. Backing Bar & Diamond Mount
  var backingBars = bannerEl.querySelectorAll('.banner-bar-bottom, .diamond-backing-bar');
  for (var k = 0; k < backingBars.length; k++) {
    backingBars[k].style.filter = filterVal;
  }

  var diamondMounts = bannerEl.querySelectorAll('.diamond-mount, .diamond-mount-frame');
  for (var m = 0; m < diamondMounts.length; m++) {
    diamondMounts[m].style.filter = filterVal + ' drop-shadow(0 3px 6px rgba(0,0,0,0.95))';
  }

  // 5. Center Diamond Faceted Gem Texture
  var gemTextures = bannerEl.querySelectorAll('.gem-texture-img');
  var gemFilterVal = theme.gemFilter || filterVal;
  for (var n = 0; n < gemTextures.length; n++) {
    gemTextures[n].style.filter = gemFilterVal;
  }

  // 6. Title and Subtitle Text Colors
  var nameEl = bannerEl.querySelector('.item-name');
  if (nameEl && theme.nameColor) {
    nameEl.style.color = theme.nameColor;
  }

  var baseEl = bannerEl.querySelector('.item-base');
  if (baseEl && theme.nameColor) {
    baseEl.style.color = theme.nameColor;
  }
};
