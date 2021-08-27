using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace RentRefactor.Repositories
{
    public class CrmRepository<T> where T : Microsoft.Xrm.Sdk.Entity
    {
        protected IOrganizationService _service;
        protected string EntityName = typeof(T).Name.ToLower();

        public CrmRepository(IOrganizationService service)
        {
            _service = service;
        }

        protected IOrganizationService Service => _service;

        public T GetCrmEntityById(Guid id)
        {
            return (T)_service.Retrieve(EntityName, id, new ColumnSet(true));
        }

        public List<T> GetByIds(IEnumerable<Guid> recordIds, ColumnSet columns = null)
        {
            var query = new QueryExpression(EntityName)
            {
                NoLock = true,
                ColumnSet = columns ?? new ColumnSet(true),
                Criteria = {
                    Conditions = {
                        new ConditionExpression(EntityName.ToLower() + "id", ConditionOperator.In, recordIds.ToArray())
                    }
                }
            };

            return _service.RetrieveMultiple(query).Entities.Select(e => e.ToEntity<T>()).ToList();
        }

        public T GetCrmEntityById(Guid id, ColumnSet columns)
        {
            return (T)_service.Retrieve(EntityName, id, columns);
        }

        public IList<T> GetEntitiesByField(string field, object value, ColumnSet columns)
        {
            var queryByAttribute = new QueryByAttribute(EntityName)
            {
                ColumnSet = columns,
                Attributes = { field },
                Values = { value }
            };

            return _service.RetrieveMultiple(queryByAttribute).Entities.Select(e => e.ToEntity<T>()).ToList();
        }

        public IList<T> GetEntitiesByField(string field, object value, ColumnSet columns, OrderExpression order)
        {
            var queryByAttribute = new QueryByAttribute(EntityName)
            {
                ColumnSet = columns,
                Attributes = { field },
                Values = { value },
                Orders = { order }
            };

            return _service.RetrieveMultiple(queryByAttribute).Entities.Select(e => e.ToEntity<T>()).ToList();
        }


        public IList<T> GetEntitiesByFieldActive(string field, object value, ColumnSet columns, OrderExpression order)
        {
            var queryByAttribute = new QueryByAttribute(EntityName)
            {
                ColumnSet = columns,
                Attributes = { field, "statecode" },
                Values = { value, 0 },
                Orders = { order }
            };

            return _service.RetrieveMultiple(queryByAttribute).Entities.Select(e => e.ToEntity<T>()).ToList();
        }

        public EntityCollection GetAll(PagingInfo pagingInfo, ColumnSet columnSet = null)
        {
            if (pagingInfo == null)
                throw new ArgumentNullException("pagingInfo");
            columnSet = columnSet ?? null;

            QueryExpression query = new QueryExpression(EntityName)
            {
                ColumnSet = columnSet,
                PageInfo = pagingInfo,
                Orders =
                {
                    new OrderExpression("createdon", OrderType.Ascending)
                },
                Distinct = true
            };

            return Service.RetrieveMultiple(query);
        }

        public IList<T> ExecuteRequest(QueryBase query)
        {
            return _service.RetrieveMultiple(query).Entities.Cast<T>().ToList();
        }

        protected OrganizationResponse Execute(OrganizationRequest query)
        {
            return _service.Execute(query);
        }

        public void Update(T entity)
        {
            _service.Update(entity.ToEntity<Entity>());
        }

        public Guid Create(T entity)
        {
            return _service.Create(entity.ToEntity<Entity>());
        }

        public void Delete(Guid id)
        {
            _service.Delete(EntityName, id);
        }
    }
}
