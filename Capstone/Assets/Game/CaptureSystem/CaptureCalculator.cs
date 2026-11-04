using System;

namespace Capstone.Game.CaptureSystem {
    public enum CaptureMajorStatus { None, Paralyzed, Burned, Poisoned, Asleep, Frozen }
    public enum CaptureBallCondition { None, Quick, Timer }
    public enum CaptureFailure {
        None, InvalidData, NotWild, ProtectedTarget, Defeated, NoTarget, NoBall, MissingConfiguration,
        NoRoom, Busy, SaveFailed, MissingServices, TurnUnavailable, DexUnavailable, AlreadyOwned
    }
    public interface ICaptureRandom { double NextFloat01(); }
    public sealed class SeededCaptureRandom : ICaptureRandom {
        readonly Random random;
        public SeededCaptureRandom(int seed) { random = new Random(seed); }
        public double NextFloat01() => random.NextDouble();
    }
    public readonly struct CaptureBallRules {
        public readonly double Multiplier;
        public readonly bool Guaranteed;
        public readonly CaptureBallCondition Condition;
        public CaptureBallRules(double multiplier, bool guaranteed = false, CaptureBallCondition condition = CaptureBallCondition.None) {
            Multiplier = multiplier; Guaranteed = guaranteed; Condition = condition;
        }
    }
    public readonly struct CaptureContext {
        public readonly double Health, MaxHealth;
        public readonly int BaseCatchRate, BattleTurnIndex, CaughtUniqueSpeciesCount;
        public readonly CaptureMajorStatus Status;
        public readonly CaptureBallRules Ball;
        public readonly bool Allowed, EnableCritical, HasCatchingCharm;
        public CaptureContext(double health, double maxHealth, int baseCatchRate, CaptureBallRules ball,
            CaptureMajorStatus status = CaptureMajorStatus.None, bool allowed = true, int battleTurnIndex = 0,
            bool enableCritical = false, int caughtUniqueSpeciesCount = -1, bool hasCatchingCharm = false) {
            Health = health; MaxHealth = maxHealth; BaseCatchRate = baseCatchRate; Ball = ball; Status = status;
            Allowed = allowed; BattleTurnIndex = battleTurnIndex; EnableCritical = enableCritical;
            CaughtUniqueSpeciesCount = caughtUniqueSpeciesCount; HasCatchingCharm = hasCatchingCharm;
        }
    }
    // Presentation receives only this decision, never the numeric odds or random rolls.
    public readonly struct CaptureRoll {
        public readonly bool Success, IsCritical;
        public readonly int ShakeCount;
        public CaptureRoll(bool success, bool critical, int shakes) { Success = success; IsCritical = critical; ShakeCount = shakes; }
    }
    internal readonly struct CaptureOdds {
        public readonly double ModifiedRate, ShakeChance, CriticalChance;
        public double NormalChance => Math.Pow(ShakeChance, 4);
        public double FinalChance => (1 - CriticalChance) * NormalChance + CriticalChance * ShakeChance;
        public CaptureOdds(double rate, double shake, double critical) { ModifiedRate = rate; ShakeChance = shake; CriticalChance = critical; }
    }
    public static class CaptureCalculator {
        static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        internal static bool TryOdds(in CaptureContext c, out CaptureOdds odds, out CaptureFailure failure) {
            odds = default; failure = CaptureFailure.None;
            if (!c.Allowed) { failure = CaptureFailure.ProtectedTarget; return false; }
            if (!Finite(c.Health) || !Finite(c.MaxHealth) || c.MaxHealth <= 0 || c.Health > c.MaxHealth
                || c.BaseCatchRate < 1 || c.BaseCatchRate > 255 || !Finite(c.Ball.Multiplier) || c.Ball.Multiplier <= 0
                || !Enum.IsDefined(typeof(CaptureMajorStatus), c.Status) || !Enum.IsDefined(typeof(CaptureBallCondition), c.Ball.Condition)) {
                failure = CaptureFailure.InvalidData; return false;
            }
            if (c.Health <= 0) { failure = CaptureFailure.Defeated; return false; }
            double ball = c.Ball.Multiplier;
            if (c.Ball.Condition != CaptureBallCondition.None) {
                if (c.BattleTurnIndex < 1) { failure = CaptureFailure.TurnUnavailable; return false; }
                ball *= c.Ball.Condition == CaptureBallCondition.Quick
                    ? (c.BattleTurnIndex == 1 ? 5 : 1) : Math.Min(1 + .3 * (c.BattleTurnIndex - 1), 4);
            }
            double status = c.Status == CaptureMajorStatus.Asleep || c.Status == CaptureMajorStatus.Frozen ? 2.5
                : c.Status == CaptureMajorStatus.None ? 1 : 1.5;
            double a = (1 - 2 * (c.Health / c.MaxHealth) / 3) * c.BaseCatchRate * ball * status;
            if (!Finite(a) || a <= 0) { failure = CaptureFailure.InvalidData; return false; }
            if (a >= 255 || c.Ball.Guaranteed) { odds = new CaptureOdds(a, 1, 0); return true; }
            double critical = 0;
            if (c.EnableCritical) {
                if (c.CaughtUniqueSpeciesCount < 0) { failure = CaptureFailure.DexUnavailable; return false; }
                int count = c.CaughtUniqueSpeciesCount;
                double dex = count <= 30 ? 0 : count <= 150 ? .5 : count <= 300 ? 1 : count <= 450 ? 1.5 : count <= 600 ? 2 : 2.5;
                critical = Math.Min(255, Math.Max(0, Math.Floor(a * dex * (c.HasCatchingCharm ? 2 : 1) / 6))) / 256;
            }
            odds = new CaptureOdds(a, Math.Pow(a / 255, .1875), critical); return true;
        }
        public static bool TryRoll(in CaptureContext context, ICaptureRandom random, out CaptureRoll decision, out CaptureFailure failure) {
            decision = default;
            if (!TryOdds(context, out var odds, out failure)) return false;
            if (odds.ShakeChance >= 1) { decision = new CaptureRoll(true, false, 3); return true; }
            if (random == null) { failure = CaptureFailure.InvalidData; return false; }
            bool critical = odds.CriticalChance > 0 && random.NextFloat01() < odds.CriticalChance;
            int checks = critical ? 1 : 4;
            for (int i = 0; i < checks; i++) if (random.NextFloat01() >= odds.ShakeChance) {
                decision = new CaptureRoll(false, critical, critical ? 1 : i); return true;
            }
            decision = new CaptureRoll(true, critical, critical ? 1 : 3); return true;
        }
    }
    public static class CaptureMessages {
        public static string For(CaptureFailure reason) => reason switch {
            CaptureFailure.None => "Sẵn sàng bắt thú.",
            CaptureFailure.NoTarget => "Khóa một thú hoang để bắt.",
            CaptureFailure.NoBall => "Bạn không còn bóng thu phục này.",
            CaptureFailure.NoRoom => "Đội thú và Kho thú đều đã đầy.",
            CaptureFailure.Busy => "Đang thực hiện một lần bắt thú.",
            CaptureFailure.Defeated => "Không thể bắt thú đã bị hạ gục.",
            CaptureFailure.NotWild => "Mục tiêu này không phải thú hoang có thể thu phục.",
            CaptureFailure.ProtectedTarget => "Không thể thu phục mục tiêu này.",
            CaptureFailure.AlreadyOwned => "Thú này đã thuộc đội hoặc Kho thú.",
            CaptureFailure.SaveFailed => "Không thể lưu lần bắt thú. Vật phẩm và dữ liệu đã được hoàn tác.",
            CaptureFailure.TurnUnavailable => "Bóng này chưa hỗ trợ chiến đấu thời gian thực.",
            CaptureFailure.MissingConfiguration => "Chưa thể dùng bóng này với mục tiêu đã chọn.",
            CaptureFailure.DexUnavailable => "Chưa có dữ liệu cẩm nang phù hợp cho kiểu bắt này.",
            _ => "Chưa thể thực hiện bắt thú. Hãy kiểm tra lại mục tiêu và túi đồ."
        };
    }
}
