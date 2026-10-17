// Alarm Controller Constructor
function AlarmController() {
  this.alarmRules = [
    new AlarmRuleModel('r1', 2.5, 'Divine', 500, true),
    new AlarmRuleModel('r2', 1.2, 'Divine', 700, true)
  ];
  this.currentAlertData = {
    itemKey: 'chambers',
    itemName: 'CHAMBERS OF INSCRIPTION',
    itemBase: 'TEMPLE OF ATZOATL - TIER 3',
    seller: 'InscribedGod_US#1395',
    priceVal: '2.5 DIVINE',
    priceType: 'Divine',
    approx: '(≈375c)',
    goldVal: '450g'
  };
}

AlarmController.prototype.init = function() {
  this.renderAlarmRules();
};

AlarmController.prototype.initReal = function() {
  this.alarmRules = [];
  this.currentAlertData = null;
  this.renderAlarmRules();
  var card = document.querySelector('.alert-card');
  if (card) {
    card.style.display = 'none';
  }
};

AlarmController.prototype.renderAlarmRules = function() {
  var listEl = document.getElementById('alarmRulesList');
  if (!listEl) return;
  listEl.innerHTML = '';

  var self = this;
  this.alarmRules.forEach(function(rule) {
    var item = document.createElement('div');
    item.className = 'alarm-rule-item';

    var leftCol = document.createElement('div');
    leftCol.className = 'monitored-item-left';

    var cb = document.createElement('input');
    cb.type = 'checkbox';
    cb.checked = rule.active;
    cb.className = 'monitored-checkbox';
    cb.addEventListener('change', function() {
      rule.toggle();
      if (window.PoeBridge) {
        window.PoeBridge.showToast((rule.active ? 'Activated' : 'Paused') + ' alarm rule');
      }
    });

    var labelSpan = document.createElement('span');
    labelSpan.className = 'monitored-item-name';
    labelSpan.innerText = '≤ ' + rule.maxPrice + ' ' + rule.currency;

    var goldSpan = document.createElement('span');
    goldSpan.className = 'monitored-item-filter';
    goldSpan.innerText = '(Max Gold: ' + rule.maxGold + ')';

    leftCol.appendChild(cb);
    leftCol.appendChild(labelSpan);
    leftCol.appendChild(goldSpan);

    var delBtn = document.createElement('button');
    delBtn.type = 'button';
    delBtn.className = 'monitored-del-btn';
    delBtn.title = 'Delete rule';
    delBtn.innerText = '×';
    delBtn.setAttribute('data-action', 'delete-alarm');
    delBtn.setAttribute('data-id', rule.id);

    item.appendChild(leftCol);
    item.appendChild(delBtn);
    listEl.appendChild(item);
  });
};

AlarmController.prototype.addRule = function() {
  var priceInput = document.getElementById('alarmMaxPriceInput');
  var currencySelect = document.getElementById('alarmCurrencySelect');
  var goldInput = document.getElementById('alarmMaxGoldInput');

  var priceVal = priceInput ? priceInput.value.trim() : '3.0';
  var currencyVal = currencySelect ? currencySelect.value : 'Divine';
  var maxGold = goldInput ? goldInput.value.trim() : '500';

  var presetKey = window.itemController ? window.itemController.currentPresetKey : 'chambers';
  var p = dataPresets[presetKey] || dataPresets.chambers;

  var rule = new AlarmRuleModel('r_' + Date.now(), priceVal, currencyVal, maxGold, true);
  this.alarmRules.push(rule);

  if (window.PoeBridge) {
    window.PoeBridge.notifyHost({
      action: 'alarm_add',
      preset: presetKey,
      maxPrice: rule.maxPrice,
      currency: rule.currency,
      maxGold: rule.maxGold
    });
  }

  this.renderAlarmRules();
  if (window.PoeBridge) {
    window.PoeBridge.showToast('Created price alert rule for ' + p.name);
  }
};

AlarmController.prototype.removeRule = function(id) {
  this.alarmRules = this.alarmRules.filter(function(r) { return r.id !== id; });
  this.renderAlarmRules();
  if (window.PoeBridge) {
    window.PoeBridge.showToast('Removed alarm rule.');
  }
};

AlarmController.prototype.recolorAlertBanner = function(themeOrKey) {
  var theme = (typeof themeOrKey === 'string')
    ? (dataPresets[themeOrKey] || dataPresets.theme1)
    : themeOrKey;

  var alertBanner = document.getElementById('alertItemBanner');
  if (alertBanner && window.BannerRenderer) {
    BannerRenderer.applyThemeToBanner(alertBanner, theme);
  }
};

AlarmController.prototype.toHideoutFromAlert = function() {
  var seller = this.currentAlertData.seller;
  var cmd = '/hideout ' + seller;
  if (window.PoeBridge) {
    window.PoeBridge.notifyHost({ action: 'hideout', seller: seller, command: cmd });
    window.PoeBridge.showToast('Copied to clipboard: ' + cmd);
  }
};

AlarmController.prototype.dismissCurrentAlert = function() {
  if (window.viewController) {
    window.viewController.switchView('evaluate');
  }
  if (window.PoeBridge) {
    window.PoeBridge.minimizeWidget();
    window.PoeBridge.showToast('Alarm dismissed - minimizing widget');
  }
};
