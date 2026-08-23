using WaterFlow.Enums;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.EventBus;

namespace WaterFlow.Framework.Systems.GamePlay
{
    public class GameplayService : ServiceSo
    {
        protected GameState gameState;


        public virtual void SetGameState(GameState gameState)
        {
            this.gameState = gameState;
            EventBus<GameStateChangeEvent>.Raise(new GameStateChangeEvent() { gameState = gameState });
        }

        public virtual GameState GetGameState()
        {
            return gameState;
        }

        public virtual bool CanInteract()
        {
            return gameState == GameState.Playing;
        }

        public virtual bool CanCountTime()
        {
            return gameState == GameState.Playing;
        }
    }
}