using UnityEngine;

namespace VTuberReactionControllerDemo.Controller
{
    public sealed class ReactionHudPresenter : MonoBehaviour
    {
        [SerializeField] private ReactionListPresenter listPresenter;
        [SerializeField] private ReactionLogPresenter logPresenter;

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (listPresenter != null)
            {
                listPresenter.RefreshFromCurrentReactionList();
            }
        }

        public void Configure(ReactionListPresenter reactionListPresenter, ReactionLogPresenter reactionLogPresenter)
        {
            if (reactionListPresenter != null)
            {
                listPresenter = reactionListPresenter;
            }

            if (reactionLogPresenter != null)
            {
                logPresenter = reactionLogPresenter;
            }

            Refresh();
        }
    }
}
