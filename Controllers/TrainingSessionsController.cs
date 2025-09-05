using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using PiranaSecuritySystem.Models;

namespace PiranaSecuritySystem.Controllers
{
    public class TrainingSessionsController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: TrainingSessions
        public ActionResult Index()
        {
            return View(db.TrainingSessions.ToList());
        }

        // GET: TrainingSessions/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            TrainingSession session = db.TrainingSessions.Find(id);
            if (session == null) return HttpNotFound();

            return View(session);
        }

        // GET: TrainingSessions/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: TrainingSessions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Id,Title,StartDate,EndDate,Capacity,Location")] TrainingSession session)
        {
            if (ModelState.IsValid)
            {
                db.TrainingSessions.Add(session);
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(session);
        }

        // GET: TrainingSessions/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            TrainingSession session = db.TrainingSessions.Find(id);
            if (session == null) return HttpNotFound();

            return View(session);
        }

        // POST: TrainingSessions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,Title,StartDate,EndDate,Capacity,Location")] TrainingSession session)
        {
            if (ModelState.IsValid)
            {
                db.Entry(session).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(session);
        }

        // GET: TrainingSessions/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            TrainingSession session = db.TrainingSessions.Find(id);
            if (session == null) return HttpNotFound();

            return View(session);
        }

        // POST: TrainingSessions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            TrainingSession session = db.TrainingSessions.Find(id);
            db.TrainingSessions.Remove(session);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
