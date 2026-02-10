using MangaMesh.Client.Series;
using MangaMesh.Client.Subscriptions;
using MangaMesh.Client.Tracker;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace MangaMesh.Server.Controllers
{
    [Route("api/subscriptions")]
    [ApiController]
    public class SubscriptionsController : ControllerBase
    {
        private readonly ISubscriptionStore _subscriptionStore;
        private readonly ITrackerClient _trackerClient;

        public SubscriptionsController(ISubscriptionStore subscriptionStore, ITrackerClient trackerClient)
        {
            _subscriptionStore = subscriptionStore;
            _trackerClient = trackerClient;
        }

        [HttpGet("list")]
        public async Task<ActionResult<IEnumerable<SeriesSubscription>>> GetSubscriptions()
        {
            return Ok(await _subscriptionStore.GetAllAsync());
        }

        [HttpPost("subscribe")]
        public async Task<ActionResult> Subscribe([FromBody] SeriesSubscription subscription)
        {
            if (string.IsNullOrWhiteSpace(subscription.SeriesId))
                return BadRequest("SeriesId is required");

            subscription.SubscribedAt = DateTime.UtcNow;
            await _subscriptionStore.AddAsync(subscription);
            return Ok();
        }

        [HttpPost("unsubscribe")]
        public async Task<ActionResult> Unsubscribe([FromBody] SeriesSubscription subscription)
        {
             if (string.IsNullOrWhiteSpace(subscription.SeriesId))
                return BadRequest("SeriesId is required");

            await _subscriptionStore.RemoveAsync(subscription);
            return Ok();
        }

        [HttpGet("updates")]
        public async Task<ActionResult<IEnumerable<SeriesSummaryResponse>>> GetUpdates()
        {
            var subs = await _subscriptionStore.GetAllAsync();
            if (!subs.Any())
                return Ok(new List<SeriesSummaryResponse>());

            var ids = subs.Select(s => s.SeriesId).Distinct().ToArray();
            
            // Fetch series details from tracker, sorted by recent activity
            // Since we implemented 'ids' filtering and 'sort=recent', this works perfectly.
            var updates = await _trackerClient.SearchSeriesAsync("", "recent", ids);

            return Ok(updates);
        }
    }
}
