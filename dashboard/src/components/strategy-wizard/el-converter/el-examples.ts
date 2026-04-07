/**
 * EasyLanguage Example Code Snippets
 *
 * Used for testing and demo purposes in EL Converter panel.
 */

export const EL_EXAMPLES = {
  ironCondor: `inputs:
  ShortStrike(30),
  LongStrike(20),
  ProfitTarget(500),
  StopLoss(1000);

variables:
  DaysToExp(45),
  Delta(0.30);

if DaysToExp = 45 then begin
  // Sell put spread
  SellShort next bar at market;

  // Buy protective put
  BuyTocover next bar at market;
end;

if NetProfit >= ProfitTarget or NetProfit <= -StopLoss then begin
  // Close all positions
  ExitLong next bar at market;
  ExitShort next bar at market;
end;`,

  simplePutSell: `inputs:
  TargetDelta(0.30),
  DTE(45),
  ProfitTarget(300);

if DaysToExpiration = DTE then
  Sell next bar at market;

if NetProfit >= ProfitTarget then
  ExitShort next bar at market;`
}
