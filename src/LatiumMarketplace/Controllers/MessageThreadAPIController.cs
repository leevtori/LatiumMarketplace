using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Identity;

using LatiumMarketplace.Data;
using LatiumMarketplace.Models;
using LatiumMarketplace.Models.MessageViewModels;
using Microsoft.AspNetCore.SignalR.Infrastructure;
using LatiumMarketplace.Hubs;

namespace LatiumMarketplace.Controllers
{
    /// <summary>
    /// The messageThreads API tests all necessary functions needed to have a conversation. 
    /// To start a new messageThread the messageThreads API post method is tested. 
    /// To get all message threads a user participates in, such as for an inbox, 
    /// the messageThreads API get method is tested. Then to delete a conversation the messageThreads 
    /// API delete method is tested. 
    /// </summary>
    [Produces("application/json")]
    [Route("api/MessageThreadAPI")]
    public class MessageThreadAPIController : ApiHubController<Broadcaster>
    {
        private IMessageThreadRepository _messageThreadRepository;
        private IMessageRepository _messageRepository;
        private ApplicationDbContext _context;

        public MessageThreadAPIController(ApplicationDbContext context, IConnectionManager connectionManager)
            : base(connectionManager)
        {
            _context = context;
            _messageRepository = new MessageRepository(context);
            _messageThreadRepository = new MessageThreadRepository(context);
        }

        /// <summary>
        /// Get all message threads for a given user. All it needs is a specific userId.
        /// </summary>
        /// <param name="userId"></param>
        // GET: api/MessageThreadAPI
        [HttpGet]
        public IActionResult Get([FromBody]string userId = "")
        {
            IEnumerable<MessageThread> userMessageTheads = null;
            try
            {
                userMessageTheads = _messageThreadRepository.GetAllMessages(userId);
            }
            catch (KeyNotFoundException)
            {
                userMessageTheads = new List<MessageThread>();
            }
            catch
            {
                return new BadRequestObjectResult("Invalid Request");
            }
            return new OkObjectResult(userMessageTheads.ToList());
        }

        /// <summary>
        /// Get a specific messsage thread. All that is needed as an argument is the id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        // GET: api/MessageThreadAPI/5
        [HttpGet("{id}", Name = "GetMessageThread")]
        [Route("api/MessageThreadAPI/GetMessageThread")]
        public string GetMessageThread(string id)
        {
            Guid guid = Guid.Parse(id);
            var messageThread = _messageThreadRepository.GetMessageThreadByID(guid);
            return "value";
        }

        /// <summary>
        /// Add a new message thread, this will create a new message thread if needed.
        /// Although this is dependent on if there is a given messagethread already 
        /// dedicated to the asset and person who is querying the asset.
        /// </summary>
        /// <param name="input"></param>
        // POST: api/MessageThreadAPI
        [HttpPost]
        public void Post([FromBody]MessageThreadDTO input)
        {
            // The reciever will always be the seller
            Message message;
            try
            {
                var messageThreadRetrieved = _context.MessageThread.Single(m => m.asset.assetID == input.AssetId && m.SenderId == input.SenderId);
                messageThreadRetrieved.LastUpdateDate = DateTime.Now;
                message = new Message(input.Subject, input.Body);
                message.messageThread = messageThreadRetrieved;
                _messageRepository.AddMessage(message);
                _messageRepository.Save();
            }
            catch (InvalidOperationException)
            {
                message = new Message(input.Subject, input.Body);
                _messageRepository.AddMessage(message);
                MessageThread messageThread = new MessageThread(input.SenderId, input.RecieverId);
                messageThread.messages.Add(message);
                messageThread.LastUpdateDate = DateTime.Now;

                messageThread.SenderEmail = _context.User.Single(u => u.Id == input.SenderId).Email;
                messageThread.RecieverEmail = _context.User.Single(u => u.Id == input.RecieverId).Email;

                if (input.AssetId != 0)
                {
                    messageThread.asset = _context.Asset.Single(a => a.assetID == input.AssetId);
                }

                _messageThreadRepository.AddMessageThread(messageThread);
                _messageThreadRepository.Save();
            }


            var recieverUser = _context.User.Single(u => u.Id == input.RecieverId);

            // This notification redirect URL should put the user to the discussion
            string redirectURL = "/MessageThreads/Details/" + message.messageThread.id.ToString();
            Notification notification = new Notification(message.Subject, message.Body, redirectURL);
            Clients.Group(recieverUser.UserName).AddNotificationToQueue(notification);
        }
        
        /// <summary>
        /// Delete a specific thread.
        /// </summary>
        /// <param name="id"></param>
        // DELETE: api/ApiWithActions/5
        [HttpDelete("{id}")]
        public void Delete(string id)
        {
            Guid guid = Guid.Parse(id);
            _messageThreadRepository.DeleteMessageThread(guid);
            _messageThreadRepository.Save();
        }
    }
}
