using UnityEngine;

namespace VTuberReactionControllerDemo.Controller
{
    public sealed class ReactionHudPresenter : MonoBehaviour
    {
        [SerializeField] private ReactionListPresenter listPresenter;
        [SerializeField] private ReactionLogPresenter logPresenter;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Reset()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            Refresh();
        }

        public void Refresh()
        {
            if (listPresenter != null)
            {
                listPresenter.RefreshFromCurrentReactionList();
            }
        }

        private void EnsureInitialized()
        {
            if (listPresenter == null)
            {
                listPresenter = GetComponent<ReactionListPresenter>();
            }

            if (logPresenter == null)
            {
                logPresenter = GetComponent<ReactionLogPresenter>();
            }
        }
    }
}
