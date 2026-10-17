// Item Controller Constructor
function ItemController(bannerRenderer, numpadFlyout) {
  this.bannerRenderer = bannerRenderer;
  this.numpadFlyout = numpadFlyout;
  this.currentPresetKey = 'chambers';
  this.currentItem = null;
}

ItemController.prototype.loadPreset = function(key) {
  var p = dataPresets[key];
  if (!p) return;

  this.currentPresetKey = key;
  this.currentItem = new ItemModel({
    name: p.name,
    base: p.base,
    rarityKey: key,
    properties: p.props,
    modifiers: p.mods,
    ninjaPrice: p.ninja
  });

  if (window.PoeBridge) {
    window.PoeBridge.notifyHost({ action: 'preset', key: key });
  }

  document.documentElement.style.setProperty('--poe-input-border', p.bannerBorder);

  // Apply theme to main banner
  if (this.bannerRenderer) {
    this.bannerRenderer.applyTheme(p);
  }

  // Update Search Button & Results Panel borders
  var searchBtn = document.getElementById('searchMarketBtn');
  if (searchBtn) {
    searchBtn.style.background = p.bannerBg;
    searchBtn.style.borderColor = p.bannerBorder;
    searchBtn.style.color = p.nameColor;
  }

  var searchPanel = document.getElementById('searchResultsPanel');
  if (searchPanel) {
    searchPanel.style.borderColor = p.bannerBorder;
    searchPanel.style.borderBottom = 'none';
  }

  // Header Text
  var itemNameEl = document.getElementById('itemName');
  var itemBaseEl = document.getElementById('itemBase');
  var ninjaPriceEl = document.getElementById('ninjaPrice');

  if (itemNameEl) {
    itemNameEl.innerText = p.name;
    itemNameEl.style.color = p.nameColor;
  }
  if (itemBaseEl) {
    itemBaseEl.innerText = p.base;
    itemBaseEl.style.color = p.nameColor;
  }
  if (ninjaPriceEl) {
    ninjaPriceEl.innerText = p.ninja;
  }

  // Render properties & mods
  this.renderProperties(p.props || []);
  this.renderModifiers(p.mods || []);

  // Update Trade Results
  if (window.tradeController) {
    window.tradeController.renderSearchResults(key);
  }

  // Update Alarm Banner
  if (window.alarmController) {
    window.alarmController.recolorAlertBanner(key);
  }
};

ItemController.prototype.showWaitingState = function() {
  this.currentItem = null;
  this.currentPresetKey = 'theme4';

  var bannerEl = document.getElementById('itemBanner');
  if (bannerEl && window.BannerRenderer) {
    BannerRenderer.constructBannerDOM(bannerEl, "WAITING FOR ITEM", "PRESS CTRL+D IN PATH OF EXILE 1");
    BannerRenderer.applyThemeToBanner(bannerEl, dataPresets.theme4);
  }

  // Apply theme4 styling to Search Market button
  var searchBtn = document.getElementById('searchMarketBtn');
  var theme = dataPresets.theme4;
  if (searchBtn && theme) {
    searchBtn.style.background = theme.bannerBg;
    searchBtn.style.borderColor = theme.bannerBorder;
    searchBtn.style.color = theme.nameColor;
  }

  var ninjaPriceEl = document.getElementById('ninjaPrice');
  if (ninjaPriceEl) {
    ninjaPriceEl.innerText = "Hover item and press hotkey";
  }

  var propCont = document.getElementById('propertyContainer');
  if (propCont) {
    propCont.innerHTML = '<div style="color: #64748b; font-size: 11px; padding: 16px; text-align: center;">No item currently evaluated. Press CTRL+D over any in-game item.</div>';
  }

  var modCont = document.getElementById('modifierContainer');
  if (modCont) {
    modCont.innerHTML = '';
  }

  var resultsList = document.getElementById('resultsList');
  if (resultsList) {
    resultsList.innerHTML = '<div style="color: #64748b; font-size: 11px; padding: 24px; text-align: center;">Trade listings will appear here after search.</div>';
  }

  var resultsCount = document.getElementById('resultsCount');
  if (resultsCount) {
    resultsCount.innerText = '0 LISTINGS';
  }
};

ItemController.prototype.loadCustomItem = function(itemData) {
  if (!itemData) return;
  this.currentItem = (itemData instanceof ItemModel) ? itemData : new ItemModel(itemData);

  var themeKey = this.currentItem.rarityKey || 'theme4';
  var theme = dataPresets[themeKey] || dataPresets.theme4;

  if (this.bannerRenderer && theme) {
    this.bannerRenderer.applyTheme(theme);
  }

  var searchBtn = document.getElementById('searchMarketBtn');
  if (searchBtn && theme) {
    searchBtn.style.background = theme.bannerBg;
    searchBtn.style.borderColor = theme.bannerBorder;
    searchBtn.style.color = theme.nameColor;
  }

  var itemNameEl = document.getElementById('itemName');
  var itemBaseEl = document.getElementById('itemBase');
  var ninjaPriceEl = document.getElementById('ninjaPrice');

  if (itemNameEl) {
    itemNameEl.innerText = this.currentItem.name;
    itemNameEl.style.color = theme ? theme.nameColor : '#e0c548';
  }
  if (itemBaseEl) {
    itemBaseEl.innerText = this.currentItem.base;
    itemBaseEl.style.color = '#cbd5e1';
  }
  if (ninjaPriceEl) {
    ninjaPriceEl.innerText = this.currentItem.ninjaPrice || 'Price check in progress...';
  }

  // Render properties & mods
  this.renderProperties(this.currentItem.properties || []);
  this.renderModifiers(this.currentItem.modifiers || []);

  var resultsList = document.getElementById('resultsList');
  var resultsCount = document.getElementById('resultsCount');
  if (resultsList) {
    resultsList.innerHTML = '<div style="color: #94a3b8; font-size: 11px; padding: 16px; text-align: center;">Click SEARCH below to query official trade API for ' + this.currentItem.name + '</div>';
  }
  if (resultsCount) {
    resultsCount.innerText = '0 LISTINGS';
  }

  // Trigger auto-minimize countdown when item arrives
  if (window.countdownTimer) {
    window.countdownTimer.reset(12.0);
    window.countdownTimer.start();
  }
};

ItemController.prototype.renderProperties = function(props) {
  var propCont = document.getElementById('propertyContainer');
  if (!propCont) return;
  propCont.innerHTML = '';

  props.forEach(function(pr, idx) {
    var pLine = document.createElement('div');
    var isSelected = pr.active !== false;
    pLine.className = 'property-line' + (isSelected ? ' is-selected' : '');
    pLine.setAttribute('data-prop-index', idx);

    var minVal = (pr.min && pr.min !== 'null') ? pr.min : '';
    var maxVal = (pr.max && pr.max !== 'null' && pr.max !== 'max') ? pr.max : '';

    var labelWrap = document.createElement('div');
    labelWrap.className = 'prop-label-wrap';
    labelWrap.title = 'Click to toggle filter';
    labelWrap.innerHTML = '<span>' + pr.label + '</span> <span class="property-value">' + pr.val + '</span>';

    labelWrap.addEventListener('click', function() {
      pLine.classList.toggle('is-selected');
    });

    var inputPair = document.createElement('div');
    inputPair.className = 'input-pair';

    var minInput = document.createElement('input');
    minInput.type = 'text';
    minInput.className = 'poe-input';
    minInput.value = minVal;
    minInput.placeholder = 'min';

    var maxInput = document.createElement('input');
    maxInput.type = 'text';
    maxInput.className = 'poe-input';
    maxInput.value = maxVal;
    maxInput.placeholder = 'max';

    inputPair.appendChild(minInput);
    inputPair.appendChild(maxInput);

    pLine.appendChild(labelWrap);
    pLine.appendChild(inputPair);
    propCont.appendChild(pLine);
  });
};

ItemController.prototype.renderModifiers = function(mods) {
  var modCont = document.getElementById('modifierContainer');
  if (!modCont) return;
  modCont.innerHTML = '';

  mods.forEach(function(m, idx) {
    var mLine = document.createElement('div');
    var isSelected = m.active !== false;
    mLine.className = 'stat-line' + (isSelected ? ' is-selected' : '');
    mLine.setAttribute('data-mod-index', idx);

    var minVal = (m.min && m.min !== 'null') ? m.min : '';
    var maxVal = (m.max && m.max !== 'null' && m.max !== 'max') ? m.max : '';

    var labelWrap = document.createElement('div');
    labelWrap.className = 'stat-label-wrap';
    labelWrap.title = 'Click to toggle filter';
    var unscalableBadge = m.isUnscalable ? '<span class="unscalable-badge">[Unscalable]</span>' : '';
    labelWrap.innerHTML = '<span class="roll-quality">' + (m.roll || '') + '</span> <span class="stat-text">' + m.text + '</span>' + unscalableBadge;

    labelWrap.addEventListener('click', function() {
      mLine.classList.toggle('is-selected');
    });

    var inputPair = document.createElement('div');
    inputPair.className = 'input-pair';

    var minInput = document.createElement('input');
    minInput.type = 'text';
    minInput.className = 'poe-input';
    minInput.value = minVal;
    minInput.placeholder = 'min';

    var maxInput = document.createElement('input');
    maxInput.type = 'text';
    maxInput.className = 'poe-input';
    maxInput.value = maxVal;
    maxInput.placeholder = 'max';

    if (m.isUnscalable) {
      minInput.disabled = true;
      maxInput.disabled = true;
      minInput.title = 'Unscalable value';
      maxInput.title = 'Unscalable value';
      minInput.style.opacity = '0.4';
      maxInput.style.opacity = '0.4';
      minInput.style.cursor = 'not-allowed';
      maxInput.style.cursor = 'not-allowed';
    }

    inputPair.appendChild(minInput);
    inputPair.appendChild(maxInput);

    mLine.appendChild(labelWrap);
    mLine.appendChild(inputPair);
    modCont.appendChild(mLine);
  });
};
