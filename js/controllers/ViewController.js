// View Controller Constructor
function ViewController() {
  this.currentMode = 'evaluate';
  this.tabEval = null;
  this.tabLive = null;
  this.panelEval = null;
  this.panelLive = null;
  this.panelAlarm = null;
}

ViewController.prototype.init = function() {
  this.tabEval = document.getElementById('tabEvaluate');
  this.tabLive = document.getElementById('tabLive');

  this.panelEval = document.getElementById('viewEvaluatePanel');
  this.panelLive = document.getElementById('viewLivePanel');
  this.panelAlarm = document.getElementById('viewAlarmPanel');

  this.switchView('evaluate');
};

ViewController.prototype.switchView = function(mode) {
  this.currentMode = mode;
  window.currentViewMode = mode;

  if (window.PoeBridge) {
    window.PoeBridge.notifyHost({ action: 'view_changed', mode: mode });
  }

  // Toggle active tab buttons
  if (this.tabEval) this.tabEval.classList.toggle('active', mode === 'evaluate');
  if (this.tabLive) this.tabLive.classList.toggle('active', mode === 'live');

  // Toggle panel visibility
  if (this.panelEval) this.panelEval.classList.toggle('is-hidden', mode !== 'evaluate');
  if (this.panelLive) this.panelLive.classList.toggle('is-hidden', mode !== 'live');
  if (this.panelAlarm) this.panelAlarm.classList.toggle('is-hidden', mode !== 'alarm');

  // Delegate view activation hooks
  if (mode === 'live' && window.liveSearchController) {
    window.liveSearchController.renderMonitoredSearches();
  } else if (mode === 'alarm' && window.alarmController) {
    var pKey = window.itemController ? window.itemController.currentPresetKey : 'chambers';
    window.alarmController.recolorAlertBanner(pKey);
    window.alarmController.renderAlarmRules();
  }
};

ViewController.prototype.getCurrentView = function() {
  return this.currentMode;
};
