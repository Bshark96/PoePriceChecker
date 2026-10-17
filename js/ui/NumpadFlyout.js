// Virtual Numpad Flyout Constructor
function NumpadFlyout(flyoutId) {
  this.flyoutEl = document.getElementById(flyoutId);
  this.activeBox = null;
}

NumpadFlyout.prototype.open = function(e, inputEl) {
  if (e && e.preventDefault) e.preventDefault();
  this.activeBox = inputEl;
  if (this.flyoutEl) {
    this.flyoutEl.style.display = 'block';
  }
};

NumpadFlyout.prototype.close = function() {
  if (this.flyoutEl) {
    this.flyoutEl.style.display = 'none';
  }
  this.activeBox = null;
};

NumpadFlyout.prototype.pressNum = function(digit) {
  if (this.activeBox) {
    if (this.activeBox.value === 'max' || this.activeBox.value === 'null') {
      this.activeBox.value = '';
    }
    this.activeBox.value += digit;
  }
};

NumpadFlyout.prototype.clear = function() {
  if (this.activeBox) {
    this.activeBox.value = '';
  }
};
