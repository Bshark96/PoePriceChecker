// Path of Exile 1 Price Alarm Rule Model Constructor
function AlarmRuleModel(id, maxPrice, currency, maxGold, active) {
  this.id = id || ('alarm_' + Date.now() + '_' + Math.floor(Math.random() * 1000));
  this.maxPrice = parseFloat(maxPrice) || 0;
  this.currency = currency || 'Divine';
  this.maxGold = parseInt(maxGold, 10) || 500;
  this.active = active !== undefined ? !!active : true;
  this.createdAt = new Date();
}

AlarmRuleModel.prototype.evaluates = function(price, currency, gold) {
  if (!this.active) return false;
  
  var p = parseFloat(price) || 0;
  var g = parseInt(gold, 10) || 0;

  if (currency.toLowerCase() !== this.currency.toLowerCase()) {
    return false;
  }

  if (p > this.maxPrice) {
    return false;
  }

  if (g > this.maxGold) {
    return false;
  }

  return true;
};

AlarmRuleModel.prototype.toggle = function() {
  this.active = !this.active;
  return this.active;
};
